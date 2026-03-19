using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client;

/// <summary>
///     Connection manager object for horse client
/// </summary>
public class ConnectionOperator
{
    private readonly HorseClient _client;

    internal ConnectionOperator(HorseClient client)
    {
        _client = client;
    }

    #region Get Items

    /// <summary>
    ///     Gets all instances connected to server
    /// </summary>
    public Task<HorseModelResult<List<NodeInformation>>> GetInstances(CancellationToken cancellationToken = default)
    {
        HorseMessage message = new()
        {
            Type = MessageType.Server,
            ContentType = KnownContentTypes.NodeList
        };

        return _client.SendAsync<List<NodeInformation>>(message, cancellationToken);
    }

    /// <summary>
    ///     Gets all consumers of queue
    /// </summary>
    public Task<HorseModelResult<List<ClientInformation>>> GetConnectedClients(CancellationToken cancellationToken = default)
    {
        return GetConnectedClients(null, cancellationToken);
    }

    /// <summary>
    ///     Gets all consumers of queue
    /// </summary>
    public Task<HorseModelResult<List<ClientInformation>>> GetConnectedClients(string typeFilter, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new()
        {
            Type = MessageType.Server,
            ContentType = KnownContentTypes.ClientList
        };
        message.SetTarget(typeFilter);

        return _client.SendAsync<List<ClientInformation>>(message, cancellationToken);
    }

    #endregion
}