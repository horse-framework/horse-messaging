using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

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
    }
}