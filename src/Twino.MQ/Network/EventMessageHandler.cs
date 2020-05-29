using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Events;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class EventMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public EventMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            string eventName = message.Target;
            string channel = message.FindHeader(TmqHeaders.CHANNEL_NAME);
            string queue = message.FindHeader(TmqHeaders.QUEUE_ID);
            bool subscribe = message.ContentType == 1;

            switch (eventName)
            {
                case EventNames.MessageProduced:
                    break;
                
                case EventNames.ClientConnected:
                    break;
                
                case EventNames.ClientDisconnected:
                    break;
                
                case EventNames.ChannelCreated:
                    break;
                
                case EventNames.ChannelUpdated:
                    break;
                
                case EventNames.ChannelRemoved:
                    break;
                
                case EventNames.ClientJoined:
                    break;
                
                case EventNames.ClientLeft:
                    break;
                
                case EventNames.QueueCreated:
                    break;
                
                case EventNames.QueueUpdated:
                    break;
                
                case EventNames.QueueRemoved:
                    break;
                
                case EventNames.NodeConnected:
                    break;
                
                case EventNames.NodeDisconnected:
                    break;
            }
        }
        
    }
}