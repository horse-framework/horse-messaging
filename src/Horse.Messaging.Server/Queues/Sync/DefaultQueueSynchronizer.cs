using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;

namespace Horse.Messaging.Server.Queues.Sync
{
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
            if (Manager.Queue.Status == QueueStatus.Syncing)
                return false;

            try
            {
                await Manager.Queue.QueueLock.WaitAsync();
            }
            catch
            {
                try
                {
                    Manager.Queue.QueueLock.Release();
                }
                catch
                {
                }

                return false;
            }

            Status = QueueSyncStatus.Sharing;
            RemoteNode = replica;
            _syncStartDate = DateTime.UtcNow;
            Manager.Queue.SetStatus(QueueStatus.Syncing);

            _ = Task.Factory.StartNew(async () =>
            {
                while (Status == QueueSyncStatus.Sharing || DateTime.UtcNow - _syncStartDate > TimeSpan.FromMinutes(3))
                {
                    try
                    {
                        await Task.Delay(1000);
                        if (!replica.IsConnected)
                        {
                            await EndSharing();
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            });

            List<string> priorityIds = Manager.PriorityMessageStore.GetUnsafe().Select(x => x.Message.MessageId).ToList();
            List<string> msgIds = Manager.MessageStore.GetUnsafe().Select(x => x.Message.MessageId).ToList();

            List<QueueMessage> deliveringMessages = Manager.DeliveryHandler.Tracker.GetDeliveringMessages();
            foreach (QueueMessage deliveringMessage in deliveringMessages)
            {
                if (deliveringMessage.Message.HighPriority)
                    priorityIds.Add(deliveringMessage.Message.MessageId);
                else
                    msgIds.Add(deliveringMessage.Message.MessageId);
            }

            StringBuilder builder = new StringBuilder();

            if (priorityIds.Count > 0)
                builder.AppendLine(priorityIds.Aggregate((c, s) => $"{c}{Environment.NewLine}{s}"));

            builder.AppendLine();

            if (msgIds.Count > 0)
                builder.AppendLine(msgIds.Aggregate((c, s) => $"{c}{Environment.NewLine}{s}"));

            HorseMessage message = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueMessageIdList);
            message.SetStringContent(builder.ToString());

            bool result = await replica.SendMessage(message);
            return result;
        }

        /// <inheritdoc />
        public virtual Task<bool> BeginReceiving(NodeClient main)
        {
            if (Status != QueueSyncStatus.None)
                return Task.FromResult(false);

            _ = Task.Factory.StartNew(async () =>
            {
                while (Status == QueueSyncStatus.Sharing)
                {
                    try
                    {
                        await Task.Delay(1000);
                        if (!main.IsConnected || DateTime.UtcNow - _syncStartDate > TimeSpan.FromMinutes(3))
                        {
                            await EndReceiving();
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            });

            RemoteNode = main;
            Status = QueueSyncStatus.Receiving;
            _syncStartDate = DateTime.UtcNow;
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public virtual async Task ProcessMessageList(HorseMessage message)
        {
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

            List<QueueMessage> removing = new List<QueueMessage>();

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
                    removing.Add(msg);
            }

            foreach (QueueMessage msg in messages)
            {
                if (!messageIds.Contains(msg.Message.MessageId))
                    removing.Add(msg);
            }

            foreach (QueueMessage msg in removing)
                await Manager.RemoveMessage(msg);

            if (requestMessages.Count == 0)
            {
                await EndReceiving();
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

            List<QueueMessage> priorityMessages = Manager.PriorityMessageStore.GetUnsafe().ToList();
            List<QueueMessage> messages = Manager.MessageStore.GetUnsafe().ToList();
            List<QueueMessage> deliveringMessages = Manager.DeliveryHandler.Tracker.GetDeliveringMessages();

            HorseMessage response = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueMessageResponse);
            response.Content = new MemoryStream();

            foreach (string id in idList)
            {
                QueueMessage msg = messages.FirstOrDefault(x => x.Message.MessageId == id);

                if (msg == null)
                    msg = priorityMessages.FirstOrDefault(x => x.Message.MessageId == id);

                if (msg == null)
                    msg = deliveringMessages.FirstOrDefault(x => x.Message.MessageId == id);

                if (msg == null)
                    continue;

                byte[] data = HorseProtocolWriter.Create(msg.Message);
                await response.Content.WriteAsync(data, 0, data.Length);
            }

            response.CalculateLengths();
            await RemoteNode.SendMessage(response);
        }

        /// <inheritdoc />
        public virtual async Task ProcessReceivedMessages(HorseMessage message)
        {
            HorseProtocolReader reader = new HorseProtocolReader();
            message.Content.Position = 0;

            while (message.Content.Position < message.Content.Length)
            {
                HorseMessage msg = await reader.Read(message.Content);
                QueueMessage queueMessage = new QueueMessage(msg, true);
                Manager.AddMessage(queueMessage);
            }
        }

        /// <inheritdoc />
        public virtual Task EndSharing()
        {
            Status = QueueSyncStatus.None;
            _syncStartDate = DateTime.UtcNow;

            Manager.Queue.SetStatus(QueueStatus.Running);

            try
            {
                Manager.Queue.QueueLock.Release();
            }
            catch
            {
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual async Task EndReceiving()
        {
            Status = QueueSyncStatus.None;
            Manager.Queue.SetStatus(QueueStatus.Running);

            HorseMessage message = new HorseMessage(MessageType.Cluster, Manager.Queue.Name, KnownContentTypes.NodeQueueSyncCompletion);
            await RemoteNode.SendMessage(message);

            RemoteNode = null;
        }
    }
}