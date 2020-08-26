using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ResponseMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly TwinoMQ _server;

        public ResponseMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient sender, TwinoMessage message, bool fromNode)
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

            //find channel and queue
            TwinoQueue queue = _server.FindQueue(message.Target);
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