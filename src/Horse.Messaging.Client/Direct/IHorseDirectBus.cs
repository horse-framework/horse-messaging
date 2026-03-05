using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct;

/// <inheritdoc />
public interface IHorseDirectBus<TIdentifier> : IHorseDirectBus
{
}

/// <summary>
/// Bus interface for sending direct (peer-to-peer) messages and request/response calls.
/// </summary>
public interface IHorseDirectBus : IHorseConnection
{
    #region Send — raw content

    /// <summary>
    /// Sends raw binary content directly to a target client.
    /// </summary>
    /// <param name="target">Target client id, name, or type depending on resolution.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends raw binary content directly to a target client with custom headers.
    /// </summary>
    /// <param name="target">Target client id, name, or type depending on resolution.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends raw binary content to a client resolved by name.
    /// </summary>
    /// <param name="name">Target client name.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends raw binary content to a client resolved by name, with custom headers.
    /// </summary>
    /// <param name="name">Target client name.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends raw binary content to a client resolved by type.
    /// </summary>
    /// <param name="type">Target client type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends raw binary content to a client resolved by type, with custom headers.
    /// </summary>
    /// <param name="type">Target client type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Send — model

    /// <summary>
    /// Sends a model to a client resolved by name.
    /// </summary>
    /// <param name="name">Target client name.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a client resolved by name, with custom headers.
    /// </summary>
    /// <param name="name">Target client name.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a client resolved by type.
    /// </summary>
    /// <param name="type">Target client type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a client resolved by type, with custom headers.
    /// </summary>
    /// <param name="type">Target client type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a client resolved by id.
    /// </summary>
    /// <param name="id">Target client unique id.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a client resolved by id, with custom headers.
    /// </summary>
    /// <param name="id">Target client unique id.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a target resolved from the model's DirectTarget attribute without waiting for acknowledgement.
    /// </summary>
    /// <param name="model">The message model with DirectTarget attribute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Send<T>(T model, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a target resolved from the model's DirectTarget attribute.
    /// </summary>
    /// <param name="model">The message model with DirectTarget attribute.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Send<T>(T model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model to a target resolved from the model's DirectTarget attribute, with custom headers.
    /// </summary>
    /// <param name="model">The message model with DirectTarget attribute.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Send<T>(T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Request — raw content

    /// <summary>
    /// Sends a request to a target client and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a request to a target client with custom headers and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a string request to a target client and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">String content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType, string content, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a string request to a target client with custom headers and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">String content to send.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a binary request to a target client and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a binary request to a target client with custom headers and waits for a response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Application-defined content type code.</param>
    /// <param name="content">Raw binary content to send.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Request — model

    /// <summary>
    /// Sends a model-based request and waits for a typed response. Target is resolved from the model's attributes.
    /// </summary>
    /// <param name="request">The request model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> Request<TResponse>(object request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model-based request with custom headers and waits for a typed response.
    /// </summary>
    /// <param name="request">The request model.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model-based request to a specific target and waits for a typed response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Optional content type override.</param>
    /// <param name="request">The request model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a model-based request to a specific target with custom headers and waits for a typed response.
    /// </summary>
    /// <param name="target">Target client id, name, or type.</param>
    /// <param name="contentType">Optional content type override.</param>
    /// <param name="request">The request model.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion
}