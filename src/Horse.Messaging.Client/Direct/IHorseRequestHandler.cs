using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct;

/// <summary>
/// Handles Horse Requests
/// </summary>
/// <typeparam name="TRequest">Request Model</typeparam>
/// <typeparam name="TResponse">Response Model</typeparam>
public interface IHorseRequestHandler<in TRequest, TResponse>
{
    /// <summary>
    /// Handles JSON Horse request and sends JSON response
    /// </summary>
    /// <param name="request">Request model</param>
    /// <param name="rawMessage">Raw Horse message</param>
    /// <param name="client">Horse MQ connection client</param>
    /// <param name="cancellationToken">
    /// Cancelled when the client disconnects, shuts down gracefully,
    /// or the response timeout is exceeded.
    /// Pass this token to any async I/O calls inside the handler.
    /// </param>
    Task<TResponse> Handle(TRequest request, HorseMessage rawMessage, HorseClient client,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executed if an exception is thrown in Handle method
    /// </summary>
    /// <param name="exception">Thrown exception</param>
    /// <param name="request">Request model</param>
    /// <param name="rawMessage">Raw Horse message</param>
    /// <param name="client">Horse MQ connection client</param>
    /// <param name="cancellationToken">Cancellation token passed from the executor.</param>
    Task<ErrorResponse> OnError(Exception exception, TRequest request, HorseMessage rawMessage, HorseClient client,
        CancellationToken cancellationToken = default);
}