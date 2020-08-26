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
        private readonly TwinoMQ _server;

        public AcknowledgeMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            //priority has no role in ack message.
            //we are using priority for helping receiver type recognization for better performance
            if (message.HighPriority)
            {
                //target should be client
                MqClient target = _server.FindClient(message.Target);
                if (target != null)
                    return target.SendAsync(message);
            }

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
                        return target.SendAsync(message);
                }

                return Task.CompletedTask;
            }

            TwinoQueue queue = channel.FindQueue(message.ContentType);
            if (queue == null)
                return Task.CompletedTask;

            return queue.AcknowledgeDelivered(client, message);
        }
    }
}