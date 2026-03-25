using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Horse Queue Consume Context
/// </summary>
/// <typeparam name="TModel">Deserialized message type</typeparam>
public class ConsumeContext<TModel>
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
    /// Creates new consume context
    /// </summary>
    public ConsumeContext(HorseMessage message, TModel model, HorseClient client, CancellationToken cancellationToken)
    {
        Message = message;
        Model = model;
        Client = client;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Sends  acknowledge message to the consumer client for the consuming message
    /// </summary>
    public Task SendAck()
    {
        return Client.SendAck(Message, CancellationToken);
    }

    /// <summary>
    /// Sends negative acknowledge message to the consumer client for the consuming message
    /// </summary>
    public Task SendNegativeAck()
    {
        return Client.SendNegativeAck(Message, CancellationToken);
    }
}