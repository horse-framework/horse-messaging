using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Topic binding targets channels with topics.
    /// Messages are pushed to queues in channels with topic.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class TopicBinding : Binding
    {
        private Channel[] _channels;
        private DateTime _channelUpdateTime;
        private readonly TimeSpan _channelCacheDuration = TimeSpan.FromMilliseconds(250);
        private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();
        private int _roundRobinIndex = -1;

        /// <summary>
        /// Tag binding routing method
        /// </summary>
        public RouteMethod RouteMethod { get; set; }

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target is the topic of channels.
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public TopicBinding(string name, string target, ushort? contentType, int priority, BindingInteraction interaction,
                            RouteMethod routeMethod = RouteMethod.Distribute)
            : base(name, target, contentType, priority, interaction)
        {
            RouteMethod = routeMethod;
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override Task<bool> Send(MqClient sender, TmqMessage message)
        {
            if (DateTime.UtcNow - _channelUpdateTime > _channelCacheDuration)
                RefreshChannelCache();

            message.PendingAcknowledge = false;
            message.PendingResponse = false;
            if (Interaction == BindingInteraction.Acknowledge)
                message.PendingAcknowledge = true;
            else if (Interaction == BindingInteraction.Response)
                message.PendingResponse = true;

            switch (RouteMethod)
            {
                case RouteMethod.Distribute:
                    return SendDistribute(message);

                case RouteMethod.OnlyFirst:
                    return SendOnlyFirst(message);

                case RouteMethod.RoundRobin:
                    return SendRoundRobin(message);

                default:
                    return Task.FromResult(false);
            }
        }

        private async Task<bool> SendDistribute(TmqMessage message)
        {
            ushort queueId = ContentType.HasValue ? ContentType.Value : message.ContentType;
            bool sent = false;
            foreach (Channel channel in _channels)
            {
                ChannelQueue queue = channel.FindQueue(queueId);
                if (queue == null)
                {
                    if (!Router.Server.Options.AutoQueueCreation)
                        continue;

                    queue = await channel.CreateQueue(queueId, channel.Options, message, Router.Server.DeliveryHandlerFactory);
                }

                string messageId = sent || Interaction == BindingInteraction.None
                                       ? message.MessageId
                                       : _idGenerator.Create();

                if (!sent)
                    sent = true;

                TmqMessage msg = message.Clone(true, true, messageId);
                QueueMessage queueMessage = new QueueMessage(msg);
                queue.AddMessage(queueMessage);
            }

            return sent;
        }

        private async Task<bool> SendRoundRobin(TmqMessage message)
        {
            Interlocked.Increment(ref _roundRobinIndex);
            int i = _roundRobinIndex;

            if (i >= _channels.Length)
            {
                _roundRobinIndex = 0;
                i = 0;
            }

            if (_channels.Length == 0)
                return false;

            Channel channel = _channels[i];

            ushort queueId = ContentType.HasValue ? ContentType.Value : message.ContentType;
            ChannelQueue queue = channel.FindQueue(queueId);
            if (queue == null)
            {
                if (!Router.Server.Options.AutoQueueCreation)
                    return false;

                queue = await channel.CreateQueue(queueId, channel.Options, message, Router.Server.DeliveryHandlerFactory);
            }

            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return true;
        }

        private async Task<bool> SendOnlyFirst(TmqMessage message)
        {
            if (_channels.Length < 1)
                return false;

            Channel channel = _channels[0];

            ushort queueId = ContentType.HasValue ? ContentType.Value : message.ContentType;
            ChannelQueue queue = channel.FindQueue(queueId);
            if (queue == null)
            {
                if (!Router.Server.Options.AutoQueueCreation)
                    return false;

                queue = await channel.CreateQueue(queueId, channel.Options, message, Router.Server.DeliveryHandlerFactory);
            }

            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return true;
        }

        private void RefreshChannelCache()
        {
            _channelUpdateTime = DateTime.UtcNow;
            _channels = Router.Server.Channels.Where(x => x.Topic != null && Filter.CheckMatch(x.Topic, Target)).ToArray();
        }
    }
}