using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Direct message binding.
    /// Targets clients.
    /// Binding receivers are received messages as DirectMessage.
    /// </summary>
    public class DirectBinding : Binding
    {
        private DateTime _clientListUpdateTime;
        private MessagingClient[] _clients;
        private int _roundRobinIndex = -1;

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
        public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
        {
            try
            {
                MessagingClient[] clients = GetClients();
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
                        var first = clients.FirstOrDefault();
                        if (first == null)
                            return false;

                        return await first.SendAsync(message);

                    case RouteMethod.Distribute:
                        bool atLeastOneSent = false;
                        foreach (MessagingClient client in clients)
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
            catch (Exception e)
            {
                Router.Rider.SendError("BINDING_SEND", e, $"Type:Direct, Binding:{Name}");
                return false;
            }
        }

        private Task<bool> SendRoundRobin(HorseMessage message)
        {
            Interlocked.Increment(ref _roundRobinIndex);
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
        private MessagingClient[] GetClients()
        {
            //using cache to prevent performance hurt thousands of message per second situations
            //receivers are reloded in every second while messages are receiving
            if (DateTime.UtcNow - _clientListUpdateTime > TimeSpan.FromMilliseconds(1000))
            {
                if (Target.StartsWith("@type:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var list = Router.Rider.Client.FindClientByType(Target.Substring(6));
                    _clients = list == null ? new MessagingClient[0] : list.ToArray();
                }
                else if (Target.StartsWith("@name:", StringComparison.InvariantCultureIgnoreCase))
                {
                    var list = Router.Rider.Client.FindClientByName(Target.Substring(6));
                    _clients = list == null ? new MessagingClient[0] : list.ToArray();
                }
                else
                {
                    MessagingClient client = Router.Rider.Client.FindClient(Target);
                    _clients = client == null ? new MessagingClient[0] : new[] {client};
                }

                _clientListUpdateTime = DateTime.UtcNow;
            }

            return _clients;
        }
    }
}