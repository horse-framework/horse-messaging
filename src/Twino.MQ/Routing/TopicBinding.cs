using System;
using System.Linq;
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
        private readonly TimeSpan _channelCacheDuration = TimeSpan.FromMilliseconds(1000);
        private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();

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
        public override async Task<bool> Send(MqClient sender, TmqMessage message)
        {
            if (DateTime.UtcNow - _channelUpdateTime > _channelCacheDuration)
                RefreshChannelCache();

            message.PendingAcknowledge = false;
            message.PendingResponse = false;
            if (Interaction == BindingInteraction.Acknowledge)
                message.PendingAcknowledge = true;
            else if (Interaction == BindingInteraction.Response)
                message.PendingResponse = true;

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

        private void RefreshChannelCache()
        {
            _channelUpdateTime = DateTime.UtcNow;
            _channels = Router.Server.Channels.Where(x => x.Topic != null && Filter.CheckMatch(x.Topic, Target)).ToArray();
        }
    }
}