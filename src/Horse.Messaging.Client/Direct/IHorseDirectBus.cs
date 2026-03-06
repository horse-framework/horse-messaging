﻿using System.Collections.Generic;
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

    /// <inheritdoc cref="SendAsync(string, ushort, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge)
        => SendAsync(target, contentType, content, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content directly to a target client.
    /// </summary>
    /// <param name="target">Target client identifier. Can be a client id, <c>@name:xxx</c>, or <c>@type:xxx</c>.</param>
    /// <param name="contentType">Custom content type identifier.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the target sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendAsync(string, ushort, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendAsync(target, contentType, content, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content directly to a target client with custom headers.
    /// </summary>
    /// <param name="target">Target client identifier.</param>
    /// <param name="contentType">Custom content type identifier.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the target sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByName(string, ushort, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge)
        => SendByName(name, contentType, content, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content to a client resolved by name.
    /// </summary>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByName(string, ushort, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendByName(name, contentType, content, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content to a client resolved by name, with custom headers.
    /// </summary>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByType(string, ushort, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge)
        => SendByType(type, contentType, content, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content to a client resolved by type.
    /// </summary>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByType(string, ushort, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendByType(type, contentType, content, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends raw binary content to a client resolved by type, with custom headers.
    /// </summary>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Send — model

    /// <inheritdoc cref="SendByName{T}(string, ushort, T, bool, CancellationToken)"/>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge)
        => SendByName(name, contentType, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by name.
    /// </summary>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByName{T}(string, ushort, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendByName(name, contentType, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by name, with custom headers.
    /// </summary>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByType{T}(string, ushort, T, bool, CancellationToken)"/>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge)
        => SendByType(type, contentType, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by type.
    /// </summary>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendByType{T}(string, ushort, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendByType(type, contentType, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by type, with custom headers.
    /// </summary>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendById{T}(string, ushort, T, bool, CancellationToken)"/>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge)
        => SendById(id, contentType, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by id.
    /// </summary>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="SendById{T}(string, ushort, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => SendById(id, contentType, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model to a client resolved by id, with custom headers.
    /// </summary>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Send{T}(T, CancellationToken)"/>
    Task<HorseResult> Send<T>(T model) where T : class
        => Send(model, CancellationToken.None);

    /// <summary>
    /// Sends a model to a target resolved from the model's <c>[DirectTarget]</c> attribute without waiting for acknowledgement.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Send<T>(T model, CancellationToken cancellationToken);

    /// <inheritdoc cref="Send{T}(T, bool, CancellationToken)"/>
    Task<HorseResult> Send<T>(T model, bool waitForAcknowledge) where T : class
        => Send(model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Sends a model to a target resolved from the model's <c>[DirectTarget]</c> attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the target sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Send<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="Send{T}(T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Send<T>(T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Send(model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model to a target resolved from the model's <c>[DirectTarget]</c> attribute, with custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForAcknowledge">If <c>true</c>, blocks until the target sends an acknowledgement. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Send<T>(T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Request — raw content

    /// <inheritdoc cref="Request(string, ushort, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType)
        => Request(target, contentType, CancellationToken.None);

    /// <summary>
    /// Sends a request to a target client and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request(string, ushort, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Request(target, contentType, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a request to a target client with custom headers and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request(string, ushort, string, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType, string content)
        => Request(target, contentType, content, CancellationToken.None);

    /// <summary>
    /// Sends a string request to a target client and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, string content, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request(string, ushort, string, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Request(target, contentType, content, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a string request to a target client with custom headers and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request(string, ushort, MemoryStream, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content)
        => Request(target, contentType, content, CancellationToken.None);

    /// <summary>
    /// Sends a binary request to a target client and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request(string, ushort, MemoryStream, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Request(target, contentType, content, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a binary request to a target client with custom headers and waits for a response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion

    #region Request — model

    /// <inheritdoc cref="Request{TResponse}(object, CancellationToken)"/>
    Task<HorseResult<TResponse>> Request<TResponse>(object request)
        => Request<TResponse>(request, CancellationToken.None);

    /// <summary>
    /// Sends a model-based request and waits for a typed response. Target is resolved from the model's attributes.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TResponse>(object request, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request{TResponse}(object, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Request<TResponse>(request, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model-based request with custom headers and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request{TResponse}(string, ushort?, object, CancellationToken)"/>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request)
        => Request<TResponse>(target, contentType, request, CancellationToken.None);

    /// <summary>
    /// Sends a model-based request to a specific target and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request, CancellationToken cancellationToken);

    /// <inheritdoc cref="Request{TResponse}(string, ushort?, object, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Request<TResponse>(target, contentType, request, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Sends a model-based request to a specific target with custom headers and waits for a typed response.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    #endregion
}

