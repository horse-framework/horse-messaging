using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Tag binding targets channel queues with tag name.
    /// Binding can send message to multiple queues with same tag name.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class TagBinding : Binding
    {
        private ChannelQueue[] _targetQueues;
        private DateTime _queueUpdateTime;
        private readonly TimeSpan _queueCacheDuration = TimeSpan.FromMilliseconds(1000);
        private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();

        /// <summary>
        /// Tag binding routing method
        /// </summary>
        public RouteMethod RouteMethod { get; set; }

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target is the tag of channels..
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public TagBinding(string name, string target, ushort? contentType, int priority, BindingInteraction interaction,
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
            if (DateTime.UtcNow - _queueUpdateTime > _queueCacheDuration)
                RefreshQueueCache();

            message.PendingAcknowledge = false;
            message.PendingResponse = false;
            if (Interaction == BindingInteraction.Acknowledge)
                message.PendingAcknowledge = true;
            else if (Interaction == BindingInteraction.Response)
                message.PendingResponse = true;

            bool sent = false;
            foreach (ChannelQueue queue in _targetQueues)
            {
                string messageId = sent || Interaction == BindingInteraction.None
                                       ? message.MessageId
                                       : _idGenerator.Create();

                if (!sent)
                    sent = true;

                TmqMessage msg = message.Clone(true, true, messageId);
                QueueMessage queueMessage = new QueueMessage(msg);
                queue.AddMessage(queueMessage);
            }

            return Task.FromResult(sent);
        }

        private void RefreshQueueCache()
        {
            _queueUpdateTime = DateTime.UtcNow;
            List<ChannelQueue> list = new List<ChannelQueue>();

            foreach (Channel channel in Router.Server.Channels)
            {
                IEnumerable<ChannelQueue> queues = channel.QueuesClone.Where(x => x.TagName != null && Filter.CheckMatch(x.TagName, Target));
                foreach (ChannelQueue queue in queues)
                    list.Add(queue);
            }

            _targetQueues = list.ToArray();
        }
    }
}