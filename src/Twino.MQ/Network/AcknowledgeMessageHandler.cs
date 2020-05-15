using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class AcknowledgeMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public AcknowledgeMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);
            if (channel == null)
            {
                //if high prio, dont try to find client again
                if (!message.HighPriority)
                {
                    //target should be client
                    MqClient target = _server.FindClient(message.Target);
                    if (target != null)
                        await target.SendAsync(message);
                }

                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);
            if (queue == null)
                return;

            await queue.AcknowledgeDelivered(client, message);
        }
    }
}