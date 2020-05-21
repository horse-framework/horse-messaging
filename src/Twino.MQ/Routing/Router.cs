using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Twino MQ Router object.
    /// A router, routes messages to its' bindings
    /// </summary>
    public class Router
    {
        #region Properties

        /// <summary>
        /// The server that router is defined
        /// </summary>
        public MqServer Server { get; }

        /// <summary>
        /// Route name.
        /// Must be unique.
        /// Can't include " ", "*" or ";"
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// If true, messages are routed to bindings.
        /// If false, messages are not routed.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Route method. Defines how messages will be routed.
        /// </summary>
        public RouteMethod Method { get; }

        /// <summary>
        /// Bindings for the router
        /// </summary>
        public Binding[] Bindings { get; private set; } = new Binding[0];

        /// <summary>
        /// Used for round robin routing.
        /// The index value of the binding received last message.
        /// </summary>
        private volatile int _lastRoutedIndex = -1;

        #endregion

        /// <summary>
        /// Creates new router on the server
        /// </summary>
        public Router(MqServer server, string name, RouteMethod method)
        {
            Server = server;
            IsEnabled = true;
            Name = name;
            Method = method;
        }

        #region Add - Remove

        /// <summary>
        /// Adds new binding to router
        /// </summary>
        public bool AddBinding(Binding binding)
        {
            if (Bindings.Any(x => x.Name.Equals(binding.Name)))
                return false;

            List<Binding> list = Bindings.ToList();
            list.Add(binding);

            binding.Router = this;
            Bindings = list.OrderByDescending(x => x.Priority).ToArray();
            return true;
        }

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        public void RemoveBinding(string bindingName)
        {
            if (!Bindings.Any(x => x.Name.Equals(bindingName)))
                return;

            List<Binding> list = Bindings.ToList();
            Binding binding = list.FirstOrDefault(x => x.Name == bindingName);
            if (binding == null)
                return;

            list.Remove(binding);

            binding.Router = null;
            Bindings = list.OrderByDescending(x => x.Priority).ToArray();
        }

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        public void RemoveBinding(Binding binding)
        {
            if (!Bindings.Contains(binding))
                return;

            List<Binding> list = Bindings.ToList();
            if (binding == null)
                return;

            list.Remove(binding);
            Bindings = list.OrderByDescending(x => x.Priority).ToArray();
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to router
        /// </summary>
        public Task<bool> Push(MqClient sender, TmqMessage message)
        {
            if (!IsEnabled || Bindings.Length == 0)
                return Task.FromResult(false);

            switch (Method)
            {
                case RouteMethod.Distribute:
                    return Distribute(sender, message);

                case RouteMethod.OnlyFirst:
                    return OnlyFirst(sender, message);

                case RouteMethod.RoundRobin:
                    return RoundRobin(sender, message);

                default:
                    return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Sends the message to only first binding
        /// </summary>
        private async Task<bool> OnlyFirst(MqClient sender, TmqMessage message)
        {
            int index = 0;
            bool sent;

            do
            {
                if (index >= Bindings.Length)
                    return false;

                Binding binding = Bindings[index];
                sent = await binding.Send(sender, message);
                index++;
            }
            while (!sent);

            return true;
        }

        /// <summary>
        /// Distributes the message to all bindings
        /// </summary>
        private async Task<bool> Distribute(MqClient sender, TmqMessage message)
        {
            bool atLeastOneSent = false;

            foreach (Binding binding in Bindings)
            {
                bool sent = await binding.Send(sender, message);
                if (!atLeastOneSent && sent)
                    atLeastOneSent = true;
            }

            return atLeastOneSent;
        }

        /// <summary>
        /// Sends the message to only one binding within round robin algorithm
        /// </summary>
        private async Task<bool> RoundRobin(MqClient sender, TmqMessage message)
        {
            for (int i = 0; i < Bindings.Length; i++)
            {
                _lastRoutedIndex++;
                if (_lastRoutedIndex >= Bindings.Length)
                    _lastRoutedIndex = 0;

                Binding binding = Bindings[_lastRoutedIndex];
                bool sent = await binding.Send(sender, message);
                if (sent)
                    return true;
            }

            return false;
        }

        #endregion
    }
}