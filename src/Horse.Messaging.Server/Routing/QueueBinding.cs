using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Queue message binding.
/// Targets queues.
/// Binding receivers are received messages as QueueMessage.
/// </summary>
public class QueueBinding : Binding
{
    private HorseQueue _targetQueue;
    private DateTime _queueUpdateTime;

    /// <summary>
    /// Sends the message to binding receivers
    /// </summary>
    public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        try
        {
            HorseQueue queue = await GetQueue(message);
            if (queue == null)
                return false;

            // The original message goes into the queue unless other bindings of the router use it after this one.
            // The id of a copy is created only when a copy is made.
            HorseMessage msg;
            if (Router.Method == RouteMethod.Distribute && Router.Bindings.Length > 1)
            {
                string cloneId = Interaction == BindingInteraction.Response
                    ? message.MessageId
                    : Router.Rider.MessageIdGenerator.Create();

                msg = message.Clone(true, true, cloneId);
            }
            else
                msg = message;

            msg.Type = MessageType.QueueMessage;
            msg.SetTarget(Target);
            msg.WaitResponse = Interaction == BindingInteraction.Response;

            QueueMessage queueMessage = new QueueMessage(msg);
            queueMessage.Source = sender;

            PushResult result = await queue.Push(queueMessage, sender);
            return result == PushResult.Success;
        }
        catch (Exception e)
        {
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:Queue, Router:{Router.Name}, Binding:{Name}", e);
            return false;
        }
    }

    /// <summary>
    /// Gets queue.
    /// If it's not cached, finds and caches it before returns.
    /// </summary>
    /// <returns></returns>
    private async Task<HorseQueue> GetQueue(HorseMessage requestMessage)
    {
        // A removed queue is not used from the cache: it refuses every push, and a queue
        // created again with the same name would not receive messages until the cache expires
        HorseQueue cached = _targetQueue;
        if (cached != null && !cached.IsDestroyed && !cached.IsClosingForDestroy && DateTime.UtcNow - _queueUpdateTime < TimeSpan.FromMinutes(1))
            return cached;

        HorseQueue queue = Router.Rider.Queue.Find(Target);

        if (queue == null)
        {
            if (Router.Rider.Queue.Options.AutoQueueCreation)
            {
                queue = await Router.Rider.Queue.Create(Target, QueueOptions.CloneFrom(Router.Rider.Queue.Options), requestMessage, true, true);
            }
        }

        if (queue != null)
            _queueUpdateTime = DateTime.UtcNow;

        _targetQueue = queue;
        return queue;
    }
}
