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
/// Provides raw binary, typed model, and request/response publish patterns.
/// </summary>
public interface IHorseRouterBus : IHorseConnection
{
    // ──────────────────────────────────────────────────────────────────
    // Publish — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes raw binary content to a router.
    /// Pass <c>null</c> for <paramref name="messageId"/> to auto-generate one.
    /// </summary>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId = null,
        bool waitForAcknowledge = false, ushort contentType = 0,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    // ──────────────────────────────────────────────────────────────────
    // Publish — model
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes a serialized model to a router.
    /// The router name is resolved from the <typeparamref name="T"/> attribute.
    /// </summary>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a serialized model to the specified router.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a serialized model to the specified router with an explicit content type.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType = null,
        bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a serialized model to the specified router with an explicit message id and content type.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType = null,
        bool waitForAcknowledge = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default) where T : class;

    // ──────────────────────────────────────────────────────────────────
    // PublishRequest
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a raw string request to a router and waits for a response from at least one binding.
    /// The <paramref name="contentType"/> parameter disambiguates this overload from model-based variants.
    /// </summary>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType = 0,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a model request to a router and waits for a typed response.
    /// The router name is resolved from the <typeparamref name="TRequest"/> attribute.
    /// </summary>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a model request to the specified router and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType = null,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);
}