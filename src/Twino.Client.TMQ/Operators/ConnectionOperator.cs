using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.Client.TMQ.Operators
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
        public Task<TmqModelResult<List<InstanceInformation>>> GetInstances()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.InstanceList;

            return _client.SendAndGetJson<List<InstanceInformation>>(message);
        }

        /// <summary>
        /// Gets all consumers of channel
        /// </summary>
        public Task<TmqModelResult<List<ClientInformation>>> GetConnectedClients(string typeFilter = null)
        {
            TmqMessage message = new TmqMessage();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all client connected events
        /// </summary>
        public async Task<bool> OffClientConnected()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Triggers the action when a client is disconnected from the server
        /// </summary>
        public async Task<bool> OnClientDisconnected(Action<ClientEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all client disconnected events
        /// </summary>
        public async Task<bool> OffClientDisconnected()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Node Events

        /// <summary>
        /// Triggers the action when a node is connected to the server
        /// </summary>
        public async Task<bool> OnNodeConnected(Action<NodeEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all node connected events
        /// </summary>
        public async Task<bool> OffNodeConnected()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Triggers the action when a node is disconnected from the server
        /// </summary>
        public async Task<bool> OnNodeDisconnected(Action<NodeEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all node disconnected events
        /// </summary>
        public async Task<bool> OffNodeDisconnected()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}