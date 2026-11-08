using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Topic binding targets queues with topics.
/// Messages are pushed to queues with topic.
/// Binding receivers are received messages as QueueMessage.
/// </summary>
public class TopicBinding : Binding
{
    private HorseQueue[] _queues = Array.Empty<HorseQueue>();
    private DateTime _queueUpdateTime;
    private readonly TimeSpan _queueCacheDuration = TimeSpan.FromMilliseconds(250);
    private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();
    private int _roundRobinIndex = -1;

    /// <summary>
    /// Sends the message to binding receivers
    /// </summary>
    public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        try
        {
            if (DateTime.UtcNow - _queueUpdateTime > _queueCacheDuration)
                RefreshQueueCache();

            HorseQueue[] queues = _queues;
            if (queues.Length == 0)
                return false;

            switch (RouteMethod)
            {
                case RouteMethod.Distribute:
                    return await SendDistribute(sender, message, queues);

                case RouteMethod.OnlyFirst:
                    return await SendToOne(sender, message, queues, 0);

                case RouteMethod.RoundRobin:
                    uint next = unchecked((uint)Interlocked.Increment(ref _roundRobinIndex));
                    return await SendToOne(sender, message, queues, (int)(next % (uint)queues.Length));

                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:Topic, Binding:{Name}", e);
            return false;
        }
    }

    /// <summary>
    /// Pushes a copy of the message to every queue of the topic.
    /// A failing queue does not stop the others. Returns true if at least one queue accepted the message.
    /// </summary>
    private async Task<bool> SendDistribute(MessagingClient sender, HorseMessage message, HorseQueue[] queues)
    {
        bool sent = false;
        foreach (HorseQueue queue in queues)
        {
            // When a response is expected, the producer waits for its own message id. That id goes to
            // the first queue that accepts the message and the others get new ids, so only one consumer
            // answers the pending request.
            string messageId = Interaction == BindingInteraction.None || !sent
                ? message.MessageId
                : _idGenerator.Create();

            HorseMessage msg = message.Clone(true, true, messageId);
            if (await PushToQueue(sender, queue, msg))
                sent = true;
        }

        return sent;
    }

    /// <summary>
    /// Pushes the message to one queue of the topic, starting from the queue at the start index.
    /// The next queue is tried when a queue does not accept the message.
    /// </summary>
    private async Task<bool> SendToOne(MessagingClient sender, HorseMessage message, HorseQueue[] queues, int start)
    {
        // The original message goes into the queue unless other bindings of the router use it after this one
        HorseMessage msg = IsSharedWithOtherBindings()
            ? message.Clone(true, true, message.MessageId)
            : message;

        for (int i = 0; i < queues.Length; i++)
        {
            // A rejected push may still have changed or kept the message, so the next queue gets a copy
            if (i > 0)
                msg = message.Clone(true, true, message.MessageId);

            HorseQueue queue = queues[(start + i) % queues.Length];
            if (await PushToQueue(sender, queue, msg))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pushes the message through the queue's push pipeline
    /// (status and limit checks, persistence, cluster replication, events)
    /// </summary>
    private async Task<bool> PushToQueue(MessagingClient sender, HorseQueue queue, HorseMessage msg)
    {
        try
        {
            msg.Type = MessageType.QueueMessage;
            msg.SetTarget(queue.Name);
            msg.WaitResponse = Interaction == BindingInteraction.Response;

            QueueMessage queueMessage = new QueueMessage(msg);
            queueMessage.Source = sender;

            PushResult result = await queue.Push(queueMessage, sender);
            return result == PushResult.Success;
        }
        catch (Exception e)
        {
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:Topic, Binding:{Name}, Queue:{queue.Name}", e);
            return false;
        }
    }

    private bool IsSharedWithOtherBindings()
    {
        Router router = Router;
        return router.Method == RouteMethod.Distribute && router.Bindings.Length > 1;
    }

    private void RefreshQueueCache()
    {
        _queueUpdateTime = DateTime.UtcNow;
        _queues = Router.Rider.Queue.Queues.Where(x => x.Topic != null && Filter.CheckMatch(x.Topic, Target)).ToArray();
    }
}
