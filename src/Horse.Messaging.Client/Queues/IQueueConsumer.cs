using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Queue Consumer implementation.
/// </summary>
/// <typeparam name="TModel">Model type</typeparam>
public interface IQueueConsumer<in TModel>
{
    /// <summary>
    /// Consumes a message
    /// </summary>
    /// <param name="message">Raw Horse message</param>
    /// <param name="model">Deserialized model</param>
    /// <param name="client">Connection client object</param>
    /// <param name="cancellationToken">
    /// Cancelled when the client disconnects or shuts down gracefully.
    /// Pass this token to any async I/O calls (HttpClient, EF Core, etc.) inside the handler.
    /// </param>
    Task Consume(HorseMessage message, TModel model, HorseClient client,
        CancellationToken cancellationToken = default);
}