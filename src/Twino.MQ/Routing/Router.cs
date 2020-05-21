using System;
using System.Collections;
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
        /// Route name.
        /// Must be unique.
        /// Can't include " ", "*" or ";"
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If true, messages are routed to bindings.
        /// If false, messages are not routed.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Route method. Defines how messages will be routed.
        /// </summary>
        public RouteMethod Method { get; set; }

        /// <summary>
        /// Bindings for the router
        /// </summary>
        public Binding[] Bindings { get; private set; }

        /// <summary>
        /// Used for round robin routing.
        /// The index value of the binding received last message.
        /// </summary>
        private volatile int _lastRoutedIndex = -1;

        #endregion

        #region Add - Remove

        /// <summary>
        /// Adds new binding to router
        /// </summary>
        public void AddBinding(Binding binding)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        public void RemoveBinding(string bindingName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a binding from the route
        /// </summary>
        public void RemoveBinding(Binding binding)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to router
        /// </summary>
        public async Task Push(MqClient sender, TmqMessage message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}