using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Interceptors;

/// <summary>
/// You can intercept your handlers (IQueueConsumer, IDirectConsumer or IHorseRequestHandler)
/// when a message is received in any handler.
/// </summary>
public interface IHorseInterceptor
{
    /// <summary>
    /// Intercept received horse message
    /// </summary>
    /// <param name="message">HorseMessage</param>
    /// <param name="client">HorseClient</param>
    /// <param name="cancellationToken">
    /// Cancelled when the client disconnects or shuts down gracefully.
    /// Pass this token to any async I/O calls inside the interceptor.
    /// </param>
    Task Intercept(HorseMessage message, HorseClient client,
        CancellationToken cancellationToken);
}