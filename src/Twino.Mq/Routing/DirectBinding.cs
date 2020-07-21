using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Direct message binding.
    /// Targets clients.
    /// Binding receivers are received messages as DirectMessage.
    /// </summary>
    public class DirectBinding : Binding
    {
        private DateTime _clientListUpdateTime;
        private MqClient[] _clients;
        private volatile int _roundRobinIndex = -1;

        /// <summary>
        /// Direct binding routing method
        /// </summary>
        public RouteMethod RouteMethod { get; set; }

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target should be client id.
        /// If you want to target clients by name, target should be @name:client_name.
        /// Same usage available for client type @type:type_name.
        /// Priority for router binding.
        /// </summary>
        public DirectBinding(string name, string target, int priority, BindingInteraction interaction,
                             RouteMethod routeMethod = RouteMethod.Distribute)
            : this(name, target, null, priority, interaction, routeMethod)
        {
        }

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target should be client id.
        /// If you want to target clients by name, target should be @name:client_name.
        /// Same usage available for client type @type:type_name.
        /// Content type the value how receiver will see the content type.
        /// Priority for router binding.
        /// </summary>
        public DirectBinding(string name, string target, ushort? contentType, int priority, BindingInteraction interaction,
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
            MqClient[] clients = GetClients();
            if (clients.Length == 0)
                return false;

            message.Type = MessageType.DirectMessage;
            message.SetTarget(Target);

            if (ContentType.HasValue)
                message.ContentType = ContentType.Value;

            message.PendingAcknowledge = false;
            message.PendingResponse = false;

            if (Interaction == BindingInteraction.Acknowledge)
                message.PendingAcknowledge = true;
            else if (Interaction == BindingInteraction.Response)
                message.PendingResponse = true;

            switch (RouteMethod)
            {
                case RouteMethod.OnlyFirst:
                    var first = clients.FirstOrDefault();
                    if (first == null)
                        return false;

                    return await first.SendAsync(message);

                case RouteMethod.Distribute:
                    bool atLeastOneSent = false;
                    foreach (MqClient client in clients)
                    {
                        bool sent = await client.SendAsync(message);
                        if (sent && !atLeastOneSent)
                            atLeastOneSent = true;
                    }

                    return atLeastOneSent;

                case RouteMethod.RoundRobin:
                    return await SendRoundRobin(message);

                default:
                    return false;
            }
        }

        private Task<bool> SendRoundRobin(TmqMessage message)
        {
            _roundRobinIndex++;
            int i = _roundRobinIndex;

            if (i >= _clients.Length)
            {
                _roundRobinIndex = 0;
                i = 0;
            }

            if (_clients.Length == 0)
                return Task.FromResult(false);

            var client = _clients[i];
            return client.SendAsync(message);
        }

        /// <summary>
        /// Gets client from cache or reload
        /// </summary>
        private MqClient[] GetClients()
        {
            //using cache to prevent performance hurt thousands of message per second situations
            //receivers are reloded in every second while messages are receiving
            if (DateTime.UtcNow - _clientListUpdateTime > TimeSpan.FromMilliseconds(1000))
            {
                if (Target.StartsWith("@type:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var list = Router.Server.FindClientByType(Target.Substring(6));
                    _clients = list == null ? new MqClient[0] : list.ToArray();
                }
                else if (Target.StartsWith("@name:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var list = Router.Server.FindClientByName(Target.Substring(6));
                    _clients = list == null ? new MqClient[0] : list.ToArray();
                }
                else
                {
                    MqClient client = Router.Server.FindClient(Target);
                    _clients = client == null ? new MqClient[0] : new[] {client};
                }

                _clientListUpdateTime = DateTime.UtcNow;
            }

            return _clients;
        }
    }
}