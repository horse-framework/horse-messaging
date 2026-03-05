using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Routers;

/// <inheritdoc />
public interface IHorseRouterBus<TIdentifier> : IHorseRouterBus
{
}
    
/// <summary>
/// Bus interface for publishing messages to Horse routers.
/// </summary>
public interface IHorseRouterBus : IHorseConnection
{
    // ── Publish — raw ──

    Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken);
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    // ── Publish — model ──

    Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    // ── PublishRequest ──

    Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken);
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken);
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken);
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
}