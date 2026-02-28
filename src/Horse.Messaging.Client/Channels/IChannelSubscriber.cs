using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Subscriber for horse channel
/// </summary>
public interface IChannelSubscriber<in TModel>
{
    /// <summary>
    /// Triggered when a message is received from a channel
    /// </summary>
    /// <param name="model">Message model</param>
    /// <param name="rawMessage">Raw message</param>
    /// <param name="client">Consumer client</param>
    /// <param name="cancellationToken">
    /// Cancelled when the client disconnects or shuts down gracefully.
    /// Pass this token to any async I/O calls inside the handler.
    /// </param>
    Task Handle(TModel model, HorseMessage rawMessage, HorseClient client,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggered when an exception is thrown while consuming the message
    /// </summary>
    /// <param name="exception">Thrown exception</param>
    /// <param name="model">Message model</param>
    /// <param name="rawMessage">Raw message</param>
    /// <param name="client">Consumer client</param>
    /// <param name="cancellationToken">Cancellation token passed from the executor.</param>
    Task Error(Exception exception, TModel model, HorseMessage rawMessage, HorseClient client,
        CancellationToken cancellationToken = default);
}
