using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Routers;

internal class HorseRouterBus<TIdentifier> : HorseRouterBus, IHorseRouterBus<TIdentifier>
{
    public HorseRouterBus(HorseClient client) : base(client) { }
}

/// <summary>
/// Default implementation of <see cref="IHorseRouterBus"/>.
/// Delegates every call to the underlying <see cref="HorseClient.Router"/> operator.
/// </summary>
public class HorseRouterBus : IHorseRouterBus
{
    private readonly HorseClient _client;

    /// <summary>
    /// Creates a new router bus backed by the given client.
    /// </summary>
    public HorseRouterBus(HorseClient client) => _client = client;

    /// <inheritdoc />
    public HorseClient GetClient() => _client;

    // ── Publish — raw ──

    /// <inheritdoc />
    public Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken)
        => _client.Router.Publish(routerName, data, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Router.Publish(routerName, data, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Router.Publish(routerName, data, messageId, waitForAcknowledge, contentType, messageHeaders, cancellationToken);

    // ── Publish — model ──

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(model, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(routerName, model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(routerName, model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(routerName, model, contentType, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class
        => _client.Router.Publish(routerName, model, messageId, contentType, waitForAcknowledge, messageHeaders, cancellationToken);

    // ── PublishRequest ──

    /// <inheritdoc />
    public Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken)
        => _client.Router.PublishRequest(routerName, message, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken)
        => _client.Router.PublishRequest(routerName, message, contentType, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Router.PublishRequest(routerName, message, contentType, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        => _client.Router.PublishRequest<TRequest, TResponse>(request, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Router.PublishRequest<TRequest, TResponse>(request, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken)
        => _client.Router.PublishRequest<TRequest, TResponse>(routerName, request, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Router.PublishRequest<TRequest, TResponse>(routerName, request, contentType, messageHeaders, cancellationToken);
}