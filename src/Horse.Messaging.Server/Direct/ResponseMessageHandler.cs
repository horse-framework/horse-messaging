using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Direct;

internal class ResponseMessageHandler : INetworkMessageHandler
{
    #region Fields

    /// <summary>
    /// Messaging Queue Server
    /// </summary>
    private readonly HorseRider _rider;

    public ResponseMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    #endregion

    public async Task Handle(MessagingClient sender, HorseMessage message, bool fromNode)
    {
        //priority has no role in ack message.
        //we are using priority for helping receiver type recognization for better performance
        if (message.HighPriority)
        {
            //target should be client
            MessagingClient target = _rider.Client.Find(message.Target);
            if (target != null)
            {
                await target.SendAsync(message);
                    
                _rider.Direct.ResponseEvent.Trigger(sender, message.Target,
                    new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
                return;
            }
        }

        //find queue
        HorseQueue queue = _rider.Queue.Find(message.Target);
        if (queue != null)
        {
            await queue.AcknowledgeDelivered(sender, message);
            return;
        }

        //if high prio, dont try to find client again
        if (!message.HighPriority)
        {
            //target should be client
            MessagingClient target = _rider.Client.Find(message.Target);
            if (target != null)
                await target.SendAsync(message);

            _rider.Direct.ResponseEvent.Trigger(sender, message.Target,
                new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
        }
    }
}