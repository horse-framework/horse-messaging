using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Router binding
    /// </summary>
    public abstract class Binding
    {
        /// <summary>
        /// Unique name of the binding
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Binding target name.
        /// For queue bindings, channel name.
        /// For direct bindings client id, type or name.
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Binding content type.
        /// Null, passes same content type from producer to receiver
        /// </summary>
        public ushort? ContentType { get; }

        /// <summary>
        /// Binding priority
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Binding interaction type
        /// </summary>
        public BindingInteraction Interaction { get; }

        /// <summary>
        /// Parent Router object of the binding
        /// </summary>
        internal Router Router { get; set; }

        /// <summary>
        /// Creates new binding
        /// </summary>
        protected Binding(string name, string target, ushort? contentType, int priority, BindingInteraction interaction)
        {
            Name = name;
            Target = target;
            ContentType = contentType;
            Priority = priority;
            Interaction = interaction;
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public abstract Task<bool> Send(MqClient sender, TmqMessage message);
    }
}