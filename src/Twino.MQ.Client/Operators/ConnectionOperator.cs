using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Client.Internal;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Client.Operators
{
    /// <summary>
    /// Connection manager object for tmq client
    /// </summary>
    public class ConnectionOperator
    {
        private readonly TmqClient _client;

        internal ConnectionOperator(TmqClient client)
        {
            _client = client;
        }

        #region Get Items

        /// <summary>
        /// Gets all instances connected to server
        /// </summary>
        public Task<TmqModelResult<List<NodeInformation>>> GetInstances()
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.InstanceList;

            return _client.SendAndGetJson<List<NodeInformation>>(message);
        }

        /// <summary>
        /// Gets all consumers of queue
        /// </summary>
        public Task<TmqModelResult<List<ClientInformation>>> GetConnectedClients(string typeFilter = null)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ClientList;
            message.SetTarget(typeFilter);

            return _client.SendAndGetJson<List<ClientInformation>>(message);
        }

        #endregion

        #region Client Events

        /// <summary>
        /// Triggers the action when a client is connected to the server
        /// </summary>
        public async Task<bool> OnClientConnected(Action<ClientEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.ClientConnected, true, null);
            if (ok)
                _client.Events.Add(EventNames.ClientConnected, null, action, typeof(ClientEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all client connected events
        /// </summary>
        public async Task<bool> OffClientConnected()
        {
            bool ok = await _client.EventSubscription(EventNames.ClientConnected, false, null);
            if (ok)
                _client.Events.Remove(EventNames.ClientConnected, null);

            return ok;
        }

        /// <summary>
        /// Triggers the action when a client is disconnected from the server
        /// </summary>
        public async Task<bool> OnClientDisconnected(Action<ClientEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.ClientDisconnected, true, null);
            if (ok)
                _client.Events.Add(EventNames.ClientDisconnected, null, action, typeof(ClientEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all client disconnected events
        /// </summary>
        public async Task<bool> OffClientDisconnected()
        {
            bool ok = await _client.EventSubscription(EventNames.ClientDisconnected, false, null);
            if (ok)
                _client.Events.Remove(EventNames.ClientDisconnected, null);

            return ok;
        }

        #endregion
    }
}