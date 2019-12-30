using System.Collections.Generic;
using Twino.MQ.Security;

namespace Twino.MQ
{
    /// <summary>
    /// Implementation container.
    /// Implementations can be kept in this container with their keys.
    /// When you need to use implementation with it's key, you can find with Get methods.
    /// </summary>
    public class ImplementationRegistry
    {
        #region Fields

        /// <summary>
        /// Message delivery handlers
        /// </summary>
        private readonly Dictionary<string, IMessageDeliveryHandler> _messageDeliveryHandlers = new Dictionary<string, IMessageDeliveryHandler>();
        
        /// <summary>
        /// Channel event handlers
        /// </summary>
        private readonly Dictionary<string, IChannelEventHandler> _channelEventHandlers = new Dictionary<string, IChannelEventHandler>();
        
        /// <summary>
        /// Channel authenticators
        /// </summary>
        private readonly Dictionary<string, IChannelAuthenticator> _channelAuthenticators = new Dictionary<string, IChannelAuthenticator>();

        #endregion

        #region Register

        /// <summary>
        /// Register a message delivery handler to the container
        /// </summary>
        public void Register(string key, IMessageDeliveryHandler value)
        {
            lock (_messageDeliveryHandlers)
                _messageDeliveryHandlers.Add(key, value);
        }

        /// <summary>
        /// Register a channel event handler to the container
        /// </summary>
        public void Register(string key, IChannelEventHandler value)
        {
            lock (_channelEventHandlers)
                _channelEventHandlers.Add(key, value);
        }

        /// <summary>
        /// Register a channel authenticator to the container
        /// </summary>
        public void Register(string key, IChannelAuthenticator value)
        {
            lock (_channelAuthenticators)
                _channelAuthenticators.Add(key, value);
        }

        #endregion

        #region Get

        /// <summary>
        /// Finds a message delivery handler with key.
        /// If not exists, returns null.
        /// </summary>
        public IMessageDeliveryHandler GetMessageDelivery(string key)
        {
            lock (_messageDeliveryHandlers)
            {
                if (_messageDeliveryHandlers.ContainsKey(key))
                    return _messageDeliveryHandlers[key];
            }

            return null;
        }

        /// <summary>
        /// Finds a channel event handler with key.
        /// If not exists, returns null.
        /// </summary>
        public IChannelEventHandler GetChannelEvent(string key)
        {
            lock (_channelEventHandlers)
            {
                if (_channelEventHandlers.ContainsKey(key))
                    return _channelEventHandlers[key];
            }

            return null;
        }

        /// <summary>
        /// Finds a channel authenticator with key.
        /// If not exists, returns null.
        /// </summary>
        public IChannelAuthenticator GetChannelAuthenticator(string key)
        {
            lock (_channelAuthenticators)
            {
                if (_channelAuthenticators.ContainsKey(key))
                    return _channelAuthenticators[key];
            }

            return null;
        }

        #endregion
    }
}