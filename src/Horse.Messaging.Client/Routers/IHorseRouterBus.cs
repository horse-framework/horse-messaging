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
    #region Publish — raw

    /// <inheritdoc cref="Publish(string, byte[], CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data)
        => Publish(routerName, data, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router.
    /// </summary>
    Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, byte[], bool, CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge)
        => Publish(routerName, data, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router, optionally waiting for acknowledgement.
    /// </summary>
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, byte[], string, bool, ushort, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Publish(routerName, data, messageId, waitForAcknowledge, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes raw byte data to a router with full control over message id, content type, and headers.
    /// </summary>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Publish — model

    /// <inheritdoc cref="Publish{T}(T, CancellationToken)"/>
    Task<HorseResult> Publish<T>(T model) where T : class
        => Publish(model, CancellationToken.None);

    /// <summary>
    /// Publishes a model to the router resolved from the model's RouterName attribute.
    /// </summary>
    Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(T, bool, CancellationToken)"/>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge) where T : class
        => Publish(model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a model to the router resolved from the model's RouterName attribute, optionally waiting for acknowledgement.
    /// </summary>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, bool, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge) where T : class
        => Publish(routerName, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router, optionally waiting for acknowledgement.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(routerName, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with custom headers.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, ushort?, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(routerName, model, contentType, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with content type override and custom headers.
    /// </summary>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Publish{T}(string, T, string, ushort?, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Publish(routerName, model, messageId, contentType, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model to a specific router with full control over message id, content type, and headers.
    /// </summary>
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
    Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest(string, string, ushort, CancellationToken)"/>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType)
        => PublishRequest(routerName, message, contentType, CancellationToken.None);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and waits for a response.
    /// </summary>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest(string, string, ushort, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest(routerName, message, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and custom headers, and waits for a response.
    /// </summary>
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
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(TRequest, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest<TRequest, TResponse>(request, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a router with custom headers and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(string, TRequest, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request)
        => PublishRequest<TRequest, TResponse>(routerName, request, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a specific router and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishRequest{TRequest, TResponse}(string, TRequest, ushort?, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishRequest<TRequest, TResponse>(routerName, request, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a model-based request to a specific router with content type and custom headers, and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion
}