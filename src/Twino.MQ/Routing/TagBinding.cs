using System;
using System.Collections.Generic;
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
    /// Tag binding targets channel queues with tag name.
    /// Binding can send message to multiple queues with same tag name.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class TagBinding : Binding
    {
        private ChannelQueue[] _targetQueues;
        private DateTime _queueUpdateTime;
        private readonly TimeSpan _queueCacheDuration = TimeSpan.FromMilliseconds(250);
        private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();
        private int _roundRobinIndex = -1;

        /// <summary>
        /// Tag binding routing method
        /// </summary>
        public RouteMethod RouteMethod { get; set; }

        /// <summary>
        /// Creates new tag binding.
        /// Name is the name of the binding.
        /// Target is the tag of queues.
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

        private Task<bool> SendDistribute(TmqMessage message)
        {
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

        private Task<bool> SendRoundRobin(TmqMessage message)
        {
            Interlocked.Increment(ref _roundRobinIndex);
            int i = _roundRobinIndex;

            if (i >= _targetQueues.Length)
            {
                _roundRobinIndex = 0;
                i = 0;
            }

            if (_targetQueues.Length == 0)
                return Task.FromResult(false);

            ChannelQueue queue = _targetQueues[i];
            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return Task.FromResult(true);
        }

        private Task<bool> SendOnlyFirst(TmqMessage message)
        {
            if (_targetQueues.Length < 1)
                return Task.FromResult(false);

            ChannelQueue queue = _targetQueues[0];
            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return Task.FromResult(true);
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