using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Routers;

internal class HorseRouterBus<TIdentifier> : HorseRouterBus, IHorseRouterBus<TIdentifier>
{
    public HorseRouterBus(HorseClient client) : base(client)
    {
    }
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
    public HorseRouterBus(HorseClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public HorseClient GetClient()
    {
        return _client;
    }

    // ──────────────────────────────────────────────────────────────────
    // Publish — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> Publish(string routerName, byte[] data, string messageId = null,
        bool waitForAcknowledge = false, ushort contentType = 0,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Router.Publish(routerName, data, messageId, waitForAcknowledge, contentType, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Publish — model
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default) where T : class
        => _client.Router.Publish<T>(model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default) where T : class
        => _client.Router.Publish<T>(routerName, model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType = null,
        bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default) where T : class
        => _client.Router.Publish<T>(routerName, model, contentType, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Publish<T>(string routerName, T model, string messageId,
        ushort? contentType = null, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default) where T : class
        => _client.Router.Publish<T>(routerName, model, messageId, contentType, waitForAcknowledge, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // PublishRequest
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType = 0,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Router.PublishRequest(routerName, message, contentType, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Router.PublishRequest<TRequest, TResponse>(request, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType = null,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Router.PublishRequest<TRequest, TResponse>(routerName, request, contentType, messageHeaders, cancellationToken);
}