using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Queue message binding.
    /// Targets channel queues.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class QueueBinding : Binding
    {
        private ChannelQueue _targetQueue;

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target should be channel name.
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public QueueBinding(string name, string target, ushort contentType, int priority, BindingInteraction interaction)
            : base(name, target, contentType, priority, interaction)
        {
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override Task<bool> Send(MqClient sender, TmqMessage message)
        {
            throw new System.NotImplementedException();
        }
    }
}