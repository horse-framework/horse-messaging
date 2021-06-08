using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Routing
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
        private int _roundRobinIndex = -1;
        private readonly object _lock = new object();

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
        public override async Task<bool> Send(MqClient sender, HorseMessage message)
        {
            try
            {
                MqClient[] clients = GetClients();
                if (clients.Length == 0)
                    return false;

                message.Type = MessageType.DirectMessage;
                message.SetTarget(Target);

                if (ContentType.HasValue)
                    message.ContentType = ContentType.Value;

                message.WaitResponse = Interaction == BindingInteraction.Response;
                switch (RouteMethod)
                {
                    case RouteMethod.OnlyFirst:
                        MqClient first = clients.FirstOrDefault();

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
                        return await SendRoundRobin(clients, message);

                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                Router.Server.SendError("BINDING_SEND", e, $"Type:Direct, Binding:{Name}");
                return false;
            }
        }

        private Task<bool> SendRoundRobin(MqClient[] clients, HorseMessage message)
        {
            lock (_lock)
            {
                _roundRobinIndex++;

                if (_roundRobinIndex >= clients.Length)
                    _roundRobinIndex = 0;
            }

            MqClient client = clients[_roundRobinIndex];

            return client.SendAsync(message);
        }

        /// <summary>
        /// Gets client from cache or reload
        /// </summary>
        private MqClient[] GetClients()
        {
            MqClient[] clients;

            //using cache to prevent performance hurt thousands of message per second situations
            //receivers are reloded in every second while messages are receiving
            if (DateTime.UtcNow - _clientListUpdateTime > TimeSpan.FromMilliseconds(1000))
            {
                if (Target.StartsWith("@type:", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<MqClient> list = Router.Server.FindClientByType(Target.Substring(6));
                    clients = list == null ? Array.Empty<MqClient>() : list.ToArray();
                }
                else if (Target.StartsWith("@name:", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<MqClient> list = Router.Server.FindClientByName(Target.Substring(6));
                    clients = list == null ? Array.Empty<MqClient>() : list.ToArray();
                }
                else
                {
                    MqClient client = Router.Server.FindClient(Target);
                    clients = client == null ? Array.Empty<MqClient>() : new[] {client};
                }

                _clientListUpdateTime = DateTime.UtcNow;
            }
            else
                return _clients;

            _clients = clients;

            return clients;
        }
    }
}