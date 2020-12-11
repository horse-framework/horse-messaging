using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    internal class ResponseMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        public ResponseMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient sender, HorseMessage message, bool fromNode)
        {
            //priority has no role in ack message.
            //we are using priority for helping receiver type recognization for better performance
            if (message.HighPriority)
            {
                //target should be client
                MqClient target = _server.FindClient(message.Target);
                if (target != null)
                {
                    await target.SendAsync(message);
                    return;
                }
            }

            //find queue
            HorseQueue queue = _server.FindQueue(message.Target);
            if (queue != null)
            {
                await queue.AcknowledgeDelivered(sender, message);
                return;
            }

            //if high prio, dont try to find client again
            if (!message.HighPriority)
            {
                //target should be client
                MqClient target = _server.FindClient(message.Target);
                if (target != null)
                    await target.SendAsync(message);
            }
        }
    }
}