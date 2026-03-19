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
/// Provides overloads for raw binary and model-based messages,
/// with optional support for acknowledgement waiting, custom headers, and cancellation tokens.
/// </summary>
public interface IHorseRouterBus : IHorseConnection
{
    #region Publish — raw

    /// <inheritdoc cref="Publish(string, byte[], CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data)
        => Publish(routerName, data, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router without waiting for acknowledgement (fire-and-forget).
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, byte[], bool, CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge)
        => Publish(routerName, data, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router, optionally waiting for acknowledgement.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary content as byte array.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server (or target) sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, byte[], string, bool, ushort, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Publish(routerName, data, messageId, waitForAcknowledge, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router with full control over message id, content type, and headers.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary content as byte array.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="contentType">Custom content type identifier.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Publish — model

    /// <inheritdoc cref="Publish{T}(T, CancellationToken)"/>
    Task<HorseResult> Publish<T>(T model) where T : class
        => Publish(model, CancellationToken.None);

    /// <summary>
    /// Publishes a model to the router resolved from the model's <c>[RouterName]</c> attribute without waiting for acknowledgement.
    /// </summary>
    /// <typeparam name="T">Message model type. Must have a <c>[RouterName]</c> attribute.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(T, bool, CancellationToken)"/>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge) where T : class
        => Publish(model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a model to the router resolved from the model's attribute, optionally waiting for acknowledgement.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model with custom headers. Router name resolved from the model's attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, bool, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge) where T : class
        => Publish(routerName, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router, optionally waiting for acknowledgement.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="routerName">Target router name. Overrides the model's <c>[RouterName]</c> attribute.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(routerName, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, ushort?, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(routerName, model, contentType, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with content type override and custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="contentType">Custom content type identifier. If <c>null</c>, the content type from the model's attribute is used.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, string, ushort?, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish<T>(routerName, model, messageId, contentType, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with full control over message id, content type, and headers.
    /// This is the most complete model Publish overload.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="routerName">Target router name. If <c>null</c>, resolved from the model's <c>[RouterName]</c> attribute.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="contentType">Custom content type identifier. If <c>null</c>, the content type from the model's attribute is used.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the server sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    #endregion

    #region PublishRequest — string

    /// <inheritdoc cref="PublishRequest(string, string, CancellationToken)"/>
    Task<HorseMessage> PublishRequest(string routerName, string message)
        => PublishRequest(routerName, message, CancellationToken.None);

    /// <summary>
    /// Publishes a string request to a router and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to publish as the request body.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>The response <see cref="HorseMessage"/> from the target.</returns>
    Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest(string, string, ushort, CancellationToken)"/>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType)
        => PublishRequest(routerName, message, contentType, CancellationToken.None);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to publish as the request body.</param>
    /// <param name="contentType">Custom content type identifier.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>The response <see cref="HorseMessage"/> from the target.</returns>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest(string, string, ushort, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest(routerName, message, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and custom headers, and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to publish as the request body.</param>
    /// <param name="contentType">Custom content type identifier.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>The response <see cref="HorseMessage"/> from the target.</returns>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region PublishRequest — model

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(TRequest, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request)
        => PublishRequest<TRequest, TResponse>(request, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a router and waits for a typed response.
    /// </summary>
    /// <typeparam name="TRequest">Request model type.</typeparam>
    /// <typeparam name="TResponse">Expected response model type.</typeparam>
    /// <param name="request">The request model to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult{TResponse}"/> containing the deserialized response.</returns>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(TRequest, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest<TRequest, TResponse>(request, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a router with custom headers and waits for a typed response.
    /// </summary>
    /// <typeparam name="TRequest">Request model type.</typeparam>
    /// <typeparam name="TResponse">Expected response model type.</typeparam>
    /// <param name="request">The request model to send.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult{TResponse}"/> containing the deserialized response.</returns>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(string, TRequest, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request)
        => PublishRequest<TRequest, TResponse>(routerName, request, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a specific router and waits for a typed response.
    /// </summary>
    /// <typeparam name="TRequest">Request model type.</typeparam>
    /// <typeparam name="TResponse">Expected response model type.</typeparam>
    /// <param name="routerName">Target router name.</param>
    /// <param name="request">The request model to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult{TResponse}"/> containing the deserialized response.</returns>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(string, TRequest, ushort?, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest<TRequest, TResponse>(routerName, request, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a specific router with content type and custom headers, and waits for a typed response.
    /// </summary>
    /// <typeparam name="TRequest">Request model type.</typeparam>
    /// <typeparam name="TResponse">Expected response model type.</typeparam>
    /// <param name="routerName">Target router name.</param>
    /// <param name="request">The request model to send.</param>
    /// <param name="contentType">Custom content type identifier. If <c>null</c>, the content type from the model's attribute is used.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult{TResponse}"/> containing the deserialized response.</returns>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion
}
