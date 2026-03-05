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

    /// <summary>
    /// Publishes raw byte data to a router.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary data to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes raw byte data to a router, optionally waiting for acknowledgement.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary data to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes raw byte data to a router with full control over message id, content type, and headers.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="data">Raw binary data to publish.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Publish — model

    /// <summary>
    /// Publishes a model to the router resolved from the model's RouterName attribute.
    /// </summary>
    /// <param name="model">The message model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Publishes a model to the router resolved from the model's RouterName attribute, optionally waiting for acknowledgement.
    /// </summary>
    /// <param name="model">The message model.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Publishes a model to a specific router, optionally waiting for acknowledgement.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Publishes a model to a specific router with custom headers.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Publishes a model to a specific router with content type override and custom headers.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model.</param>
    /// <param name="contentType">Optional content type override.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Publishes a model to a specific router with full control over message id, content type, and headers.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="model">The message model.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="contentType">Optional content type override.</param>
    /// <param name="waitForAcknowledge">If true, waits for the router to acknowledge.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    #endregion

    #region PublishRequest — string

    /// <summary>
    /// Publishes a string request to a router and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to send.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a string request to a router with a specific content type and custom headers, and waits for a response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="message">String content to send.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region PublishRequest — model

    /// <summary>
    /// Publishes a model-based request to a router and waits for a typed response. Router is resolved from model attributes.
    /// </summary>
    /// <param name="request">The request model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a model-based request to a router with custom headers and waits for a typed response.
    /// </summary>
    /// <param name="request">The request model.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a model-based request to a specific router and waits for a typed response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="request">The request model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a model-based request to a specific router with content type and custom headers, and waits for a typed response.
    /// </summary>
    /// <param name="routerName">Target router name.</param>
    /// <param name="request">The request model.</param>
    /// <param name="contentType">Optional content type override.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion
}