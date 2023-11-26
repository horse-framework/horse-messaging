using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Transactions;

internal class TransactionMessageHandler : INetworkMessageHandler
{
    #region Fields

    /// <summary>
    /// Messaging Queue Server
    /// </summary>
    private readonly HorseRider _rider;

    internal TransactionMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    #endregion

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        try
        {
            await HandleUnsafe(client, message);
        }
        catch (OperationCanceledException)
        {
            await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
        }
        catch
        {
            await client.SendAsync(message.CreateResponse(HorseResultCode.Failed));
        }
    }

    private Task HandleUnsafe(MessagingClient client, HorseMessage message)
    {
        switch (message.ContentType)
        {
            case KnownContentTypes.TransactionBegin:
                return _rider.Transaction.Begin(client, message);

            case KnownContentTypes.TransactionCommit:
                return _rider.Transaction.Commit(client, message);

            case KnownContentTypes.TransactionRollback:
                return _rider.Transaction.Rollback(client, message);

            default:
                throw new OperationCanceledException();
        }
    }
}