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
    private HorseQueue[] _queues;
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

            message.Type = MessageType.QueueMessage;
            message.WaitResponse = Interaction == BindingInteraction.Response;

            switch (RouteMethod)
            {
                case RouteMethod.Distribute:
                    return await SendDistribute(sender, message);

                case RouteMethod.OnlyFirst:
                    return await SendOnlyFirst(sender, message);

                case RouteMethod.RoundRobin:
                    return await SendRoundRobin(sender, message);

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

    private Task<bool> SendDistribute(MessagingClient sender, HorseMessage message)
    {
        bool sent = false;
        foreach (HorseQueue queue in _queues)
        {
            string messageId = sent || Interaction == BindingInteraction.None
                ? message.MessageId
                : _idGenerator.Create();

            if (!sent)
                sent = true;

            HorseMessage msg = message.Clone(true, true, messageId);
            msg.SetTarget(queue.Name);
                
            QueueMessage queueMessage = new QueueMessage(msg);
            queueMessage.Source = sender;

            PushResult result = queue.AddMessage(queueMessage);

            if (result != PushResult.Success)
                return Task.FromResult(false);
        }

        return Task.FromResult(sent);
    }

    private async Task<bool> SendRoundRobin(MessagingClient sender, HorseMessage message)
    {
        Interlocked.Increment(ref _roundRobinIndex);
        int i = _roundRobinIndex;

        if (i >= _queues.Length)
            _roundRobinIndex = 0;

        if (_queues.Length == 0)
            return false;

        HorseQueue queue = Router.Rider.Queue.Find(message.Target);
        if (queue == null)
        {
            if (!Router.Rider.Queue.Options.AutoQueueCreation)
                return false;

            queue = await Router.Rider.Queue.Create(message.Target,
                Router.Rider.Queue.Options,
                message,
                true,
                true);
        }

        message.SetTarget(queue.Name);
        QueueMessage queueMessage = new QueueMessage(message);
        queueMessage.Source = sender;
            
        PushResult result = queue.AddMessage(queueMessage);
        return result == PushResult.Success;
    }

    private async Task<bool> SendOnlyFirst(MessagingClient sender, HorseMessage message)
    {
        if (_queues.Length < 1)
            return false;

        HorseQueue queue = Router.Rider.Queue.Find(message.Target);
        if (queue == null)
        {
            if (!Router.Rider.Queue.Options.AutoQueueCreation)
                return false;

            queue = await Router.Rider.Queue.Create(message.Target,
                Router.Rider.Queue.Options,
                message,
                true,
                true);
        }

        message.SetTarget(queue.Name);
        QueueMessage queueMessage = new QueueMessage(message);
        queueMessage.Source = sender;
            
        queue.AddMessage(queueMessage);
        return true;
    }

    private void RefreshQueueCache()
    {
        _queueUpdateTime = DateTime.UtcNow;
        _queues = Router.Rider.Queue.Queues.Where(x => x.Topic != null && Filter.CheckMatch(x.Topic, Target)).ToArray();
    }
}