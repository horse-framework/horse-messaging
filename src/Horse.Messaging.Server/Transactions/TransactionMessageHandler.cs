using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Transactions;

internal class TransactionMessageHandler : INetworkMessageHandler
{
    private readonly HorseRider _rider;

    internal TransactionMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        HorseResultCode code;

        try
        {
            code = message.ContentType switch
            {
                KnownContentTypes.TransactionBegin => await _rider.Transaction.Begin(client, message),
                KnownContentTypes.TransactionCommit => await _rider.Transaction.Finish(client, message, ServerTransactionStatus.Commit),
                KnownContentTypes.TransactionRollback => await _rider.Transaction.Finish(client, message, ServerTransactionStatus.Rollback),
                _ => HorseResultCode.BadRequest
            };
        }
        catch (Exception e)
        {
            _rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionRequest, $"Transaction request failed: {message.Target}", e);
            code = HorseResultCode.Failed;
        }

        if (message.WaitResponse && !string.IsNullOrEmpty(message.MessageId))
            await client.SendAsync(message.CreateResponse(code));
    }
}
