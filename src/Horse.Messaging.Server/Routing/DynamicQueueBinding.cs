using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Dynamic Queue message binding.
/// Targets queues.
/// Creates queue automatically from message headers, if it's not exist.
/// Queue is searched for each published messages. This binding can send each message to different queues.
/// Binding receivers are received messages as QueueMessage.
/// </summary>
public class DynamicQueueBinding : Binding
{
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

            string messageId = Interaction == BindingInteraction.None
                ? Router.Rider.MessageIdGenerator.Create()
                : message.MessageId;

            HorseMessage msg = message.Clone(true, true, messageId);

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
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:DynamicQueue, Router:{Router.Name}, Binding:{Name}", e);
            return false;
        }
    }

    /// <summary>
    /// Gets queue.
    /// If it's not cached, finds and caches it before returns.
    /// </summary>
    /// <returns></returns>
    private async Task<HorseQueue> GetQueue(HorseMessage message)
    {
        string queueName = message.FindHeader(HorseHeaders.QUEUE_NAME);
        if (queueName == null)
            return null;

        HorseQueue queue = await Router.Rider.Queue.Create(queueName, Router.Rider.Queue.Options, message, true, true);
        return queue;
    }
}