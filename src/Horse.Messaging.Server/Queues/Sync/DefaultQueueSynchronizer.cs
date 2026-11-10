using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Sync;

/// <summary>
/// Default Queue synchronizer
/// </summary>
public class DefaultQueueSynchronizer : IQueueSynchronizer
{
    /// <inheritdoc />
    public IHorseQueueManager Manager { get; }

    /// <inheritdoc />
    public QueueSyncStatus Status { get; private set; }

    /// <inheritdoc />
    public NodeClient RemoteNode { get; private set; }

    private DateTime _syncStartDate;
    private List<QueueMessage> _additionalMessages;
    private bool _reverseMessagesReceived;

    /// <summary>
    /// Guards the sync state transitions. The end of a sync can be requested by the remote node,
    /// by the timeout watcher and by the disconnect handler at the same time; only one of them may
    /// release the queue lock.
    /// </summary>
    private readonly object _stateLock = new();

    /// <summary>
    /// Incremented for each sync. A watcher of an ended sync must not end the next one.
    /// </summary>
    private int _session;

    /// <summary>
    /// Maximum duration a sync may hold the queue lock.
    /// While the lock is held, every push to the queue waits (in Reliable mode each push waits for it).
    /// When the remote node does not complete the sync in this duration, the sync is ended and the lock is released.
    /// Default is 3 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Creates new default queue synchronizer
    /// </summary>
    public DefaultQueueSynchronizer(IHorseQueueManager manager)
    {
        Manager = manager;
    }

    /// <inheritdoc />
    public virtual async Task<bool> BeginSharing(NodeClient replica)
    {
        try
        {
            await Manager.Queue.QueueLock.WaitAsync();
        }
        catch
        {
            return false;
        }

        int session;
        lock (_stateLock)
        {
            //state is reset after the lock is taken, another sync may be running until then
            _reverseMessagesReceived = false;
            _additionalMessages = new List<QueueMessage>();
            RemoteNode = replica;
            replica.OnDisconnected += OnNodeDisconnected;
            Status = QueueSyncStatus.Sharing;
            _syncStartDate = DateTime.UtcNow;
            session = ++_session;
        }

        Manager.Queue.SetStatus(QueueStatus.Syncing);

        //the lock is held until the replica sends the completion.
        //A replica that never completes (it lost the message, failed while processing it, or has no such queue)
        //used to keep the queue in Syncing, and every push to it waiting, until the replica disconnected.
        _ = WatchSync(replica, session, QueueSyncStatus.Sharing);

        List<string> priorityIds = Manager.PriorityMessageStore.GetUnsafe().Select(x => x.Message.MessageId).ToList();
        List<string> msgIds = Manager.MessageStore.GetUnsafe().Select(x => x.Message.MessageId).ToList();

        QueueMessage processing = Manager.Queue.ProcessingMessage;
        if (processing != null)
        {
            if (processing.Message.HighPriority)
                priorityIds.Insert(0, processing.Message.MessageId);
            else
                msgIds.Insert(0, processing.Message.MessageId);
        }

        List<QueueMessage> deliveringMessages = Manager.DeliveryHandler.Tracker.GetDeliveringMessages().ToList();
        foreach (QueueMessage deliveringMessage in deliveringMessages)
        {
            if (deliveringMessage.Message.HighPriority)
                priorityIds.Insert(0, deliveringMessage.Message.MessageId);
            else
                msgIds.Insert(0, deliveringMessage.Message.MessageId);
        }

        StringBuilder builder = new StringBuilder();

        if (priorityIds.Count > 0)
            builder.AppendLine(priorityIds.Aggregate((c, s) => $"{c}{Environment.NewLine}{s}"));

        builder.AppendLine();

        if (msgIds.Count > 0)
            builder.AppendLine(msgIds.Aggregate((c, s) => $"{c}{Environment.NewLine}{s}"));

        HorseMessage message = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueMessageIdList);
        message.SetStringContent(builder.ToString());

        bool result;
        try
        {
            result = await replica.SendMessage(message);
        }
        catch
        {
            result = false;
        }

        //the replica did not get the list and will never complete the sync
        if (!result)
            EndSharing(session);

        return result;
    }

    /// <inheritdoc />
    public virtual async Task<bool> BeginReceiving(NodeClient main)
    {
        try
        {
            await Manager.Queue.QueueLock.WaitAsync();
        }
        catch
        {
            return false;
        }

        int session;
        lock (_stateLock)
        {
            //the state is set before the watcher starts.
            //The watcher used to start first, it could see the previous status and exit without watching.
            _reverseMessagesReceived = false;
            _additionalMessages = new List<QueueMessage>();
            RemoteNode = main;
            main.OnDisconnected += OnNodeDisconnected;
            Status = QueueSyncStatus.Receiving;
            _syncStartDate = DateTime.UtcNow;
            session = ++_session;
        }

        _ = WatchSync(main, session, QueueSyncStatus.Receiving);
        return true;
    }

    /// <summary>
    /// Ends the sync when the remote node disconnects or the sync takes longer than <see cref="Timeout"/>.
    /// Exits when the sync is ended by another way.
    /// </summary>
    private async Task WatchSync(NodeClient remote, int session, QueueSyncStatus status)
    {
        while (true)
        {
            try
            {
                await Task.Delay(1000);

                DateTime startDate;
                lock (_stateLock)
                {
                    if (_session != session || Status != status)
                        return;

                    startDate = _syncStartDate;
                }

                if (remote.IsConnected && DateTime.UtcNow - startDate <= Timeout)
                    continue;

                if (!remote.IsConnected)
                    Manager.Queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ClusterMessageProcess,
                        $"Queue sync for {Manager.Queue.Name} is ended, node {remote.Info?.Name} is disconnected", null);
                else
                    Manager.Queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.ClusterMessageProcess,
                        $"Queue sync for {Manager.Queue.Name} with node {remote.Info?.Name} is not completed in {Timeout}, the sync is ended", null);

                if (status == QueueSyncStatus.Sharing)
                    EndSharing(session);
                else
                    await EndReceiving(false, session);

                return;
            }
            catch
            {
            }
        }
    }

    /// <inheritdoc />
    public virtual async Task ProcessMessageList(HorseMessage message)
    {
        //a list for a sync this node is not receiving (it timed out or was never started).
        //Answering it would use a cleared RemoteNode and throw.
        if (Status != QueueSyncStatus.Receiving || RemoteNode == null)
            return;

        string[] lines = message.GetStringContent().Split(Environment.NewLine);

        bool priority = true;
        List<string> priorityIds = new List<string>();
        List<string> messageIds = new List<string>();

        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                priority = false;
                continue;
            }

            if (priority)
                priorityIds.Add(line);
            else
                messageIds.Add(line);
        }

        List<string> requestMessages = new List<string>();

        List<QueueMessage> priorityMessages = Manager.PriorityMessageStore.GetUnsafe().ToList();
        List<QueueMessage> messages = Manager.MessageStore.GetUnsafe().ToList();

        _additionalMessages = new List<QueueMessage>();

        foreach (string id in priorityIds)
        {
            bool exists = priorityMessages.Any(x => x.Message.MessageId == id);
            if (!exists)
                requestMessages.Add(id);
        }

        foreach (string id in messageIds)
        {
            bool exists = messages.Any(x => x.Message.MessageId == id);
            if (!exists)
                requestMessages.Add(id);
        }

        foreach (QueueMessage msg in priorityMessages)
        {
            if (!priorityIds.Contains(msg.Message.MessageId))
                _additionalMessages.Add(msg);
        }

        foreach (QueueMessage msg in messages)
        {
            if (!messageIds.Contains(msg.Message.MessageId))
                _additionalMessages.Add(msg);
        }

        if (requestMessages.Count == 0)
        {
            await EndReceiving(true);
            return;
        }

        HorseMessage requestMessage = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueMessageRequest);
        string content = requestMessages.Aggregate((c, s) => $"{c}{Environment.NewLine}{s}");
        requestMessage.SetStringContent(content);

        await RemoteNode.SendMessage(requestMessage);
    }

    /// <inheritdoc />
    public virtual async Task SendMessages(HorseMessage requestMessage)
    {
        string[] idList = requestMessage.GetStringContent()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        HorseMessage response = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueMessageResponse);
        await WriteMessages(response, idList);
        response.CalculateLengths();
        await RemoteNode.SendMessage(response);
    }

    /// <inheritdoc />
    public virtual async Task ProcessReceivedMessages(HorseMessage message, bool receivedFromMainNode)
    {
        if (!receivedFromMainNode)
            _reverseMessagesReceived = true;

        HorseProtocolReader reader = new HorseProtocolReader();
        message.Content.Position = 0;

        while (message.Content.Position < message.Content.Length)
        {
            HorseMessage msg = await reader.Read(message.Content);
            QueueMessage queueMessage = new QueueMessage(msg, true);
            try
            {
                if (Manager.AddMessage(queueMessage))
                    Manager.Queue.RegisterUniqueMessageId(msg.MessageId);
            }
            catch
            {
                //already added
            }
        }
    }

    /// <inheritdoc />
    public virtual Task EndSharing()
    {
        EndSharing(null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ends sharing. When session is not null, ends only if that sync is still running.
    /// </summary>
    private void EndSharing(int? session)
    {
        NodeClient remote;
        bool reverseMessagesReceived;

        lock (_stateLock)
        {
            // A completion can reach a queue this node is not sharing (roles flip at startup, or the
            // timeout already ended it). Ending again would release a QueueLock this sync no longer
            // holds, and with RemoteNode cleared it threw a NullReferenceException.
            if (Status != QueueSyncStatus.Sharing || RemoteNode == null)
                return;

            if (session.HasValue && session.Value != _session)
                return;

            Status = QueueSyncStatus.None;
            _syncStartDate = DateTime.UtcNow;
            remote = RemoteNode;
            remote.OnDisconnected -= OnNodeDisconnected;
            reverseMessagesReceived = _reverseMessagesReceived;
            _reverseMessagesReceived = false;
        }

        Manager.Queue.SetStatus(QueueStatus.Running);

        try
        {
            Manager.Queue.QueueLock.Release();
        }
        catch
        {
        }

        if (reverseMessagesReceived)
        {
            var cluster = Manager.Queue.Rider.Cluster;
            foreach (NodeClient client in cluster.Clients)
            {
                if (client.Info.Id == remote.Info.Id)
                    continue;

                HorseMessage message = new HorseMessage(MessageType.Cluster, client.Info.Id, KnownContentTypes.NodeTriggerQueueListRequest);
                _ = client.SendMessage(message);
            }
        }
    }

    private async Task WriteMessages(HorseMessage target, IEnumerable<string> idList)
    {
        QueueMessage processingMessage = Manager.Queue.ProcessingMessage;
        List<QueueMessage> priorityMessages = Manager.PriorityMessageStore.GetUnsafe().ToList();
        List<QueueMessage> messages = Manager.MessageStore.GetUnsafe().ToList();
        List<QueueMessage> deliveringMessages = Manager.DeliveryHandler.Tracker.GetDeliveringMessages().ToList();

        target.Content = new MemoryStream();

        foreach (string id in idList)
        {
            QueueMessage msg;
            if (processingMessage != null && processingMessage.Message.MessageId == id)
                msg = processingMessage;
            else
                msg = messages.FirstOrDefault(x => x.Message.MessageId == id);

            if (msg == null)
                msg = priorityMessages.FirstOrDefault(x => x.Message.MessageId == id)
                      ?? deliveringMessages.FirstOrDefault(x => x.Message.MessageId == id);

            if (msg == null)
                continue;

            byte[] data = HorseProtocolWriter.Create(msg.Message);
            await target.Content.WriteAsync(data);
        }

        target.CalculateLengths();
    }

    /// <inheritdoc />
    public virtual Task EndReceiving(bool sendLocalMessages)
    {
        return EndReceiving(sendLocalMessages, null);
    }

    /// <summary>
    /// Ends receiving. When session is not null, ends only if that sync is still running.
    /// </summary>
    private async Task EndReceiving(bool sendLocalMessages, int? session)
    {
        NodeClient remote;
        List<QueueMessage> additionalMessages;

        lock (_stateLock)
        {
            //the watcher, the disconnect handler and the main node's response may end the same sync.
            //Only the first one releases the queue lock.
            if (Status != QueueSyncStatus.Receiving || RemoteNode == null)
                return;

            if (session.HasValue && session.Value != _session)
                return;

            Status = QueueSyncStatus.None;
            remote = RemoteNode;
            RemoteNode = null;
            remote.OnDisconnected -= OnNodeDisconnected;
            additionalMessages = _additionalMessages;
            _additionalMessages = new List<QueueMessage>();
        }

        try
        {
            if (sendLocalMessages && additionalMessages != null && additionalMessages.Count > 0)
            {
                HorseMessage reverse = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueSyncReverseMessages);
                reverse.AddHeader(HorseHeaders.COUNT, additionalMessages.Count);
                reverse.Content = new MemoryStream();

                foreach (QueueMessage msg in additionalMessages)
                {
                    byte[] data = HorseProtocolWriter.Create(msg.Message);
                    await reverse.Content.WriteAsync(data);
                }

                reverse.CalculateLengths();
                reverse.AddHeader(HorseHeaders.COUNT, 0);
                await remote.SendMessage(reverse);
            }
        }
        finally
        {
            //the lock is released even if the reverse messages could not be sent
            Manager.Queue.SetStatus(QueueStatus.Running);

            try
            {
                Manager.Queue.QueueLock.Release();
            }
            catch
            {
            }
        }

        HorseMessage message = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueSyncCompletion);
        await remote.SendMessage(message);
    }

    private void OnNodeDisconnected(NodeClient nodeClient)
    {
        if (Status == QueueSyncStatus.Sharing)
            EndSharing(null);
        else if (Status == QueueSyncStatus.Receiving)
            _ = EndReceiving(false, null);
    }
}