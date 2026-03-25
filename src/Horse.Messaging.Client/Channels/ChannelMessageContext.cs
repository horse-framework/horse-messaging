using System.Threading;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Horse Channel Subscription Context
/// </summary>
/// <typeparam name="TModel">Deserialized message type</typeparam>
public class ChannelMessageContext<TModel>
{
    /// <summary>
    /// Deserialized message
    /// </summary>
    public TModel Model { get; }

    /// <summary>
    /// Raw message
    /// </summary>
    public HorseMessage Message { get; }

    /// <summary>
    /// Consumer client
    /// </summary>
    public HorseClient Client { get; }

    /// <summary>
    /// Canceled when the client disconnects or shuts down gracefully.
    /// Pass this token to any async I/O calls (HttpClient, EF Core, etc.) inside the handler.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// 0 for the first try, 1 for the second try, etc.
    /// </summary>
    public int TryCount { get; internal set; }

    /// <summary>
    /// Creates new subscription context
    /// </summary>
    public ChannelMessageContext(HorseMessage message, TModel model, HorseClient client, CancellationToken cancellationToken)
    {
        Message = message;
        Model = model;
        Client = client;
        CancellationToken = cancellationToken;
    }
}