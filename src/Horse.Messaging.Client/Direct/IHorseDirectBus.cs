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
/// Provides raw binary, typed model, and request/response patterns.
/// </summary>
public interface IHorseDirectBus : IHorseConnection
{
    // ──────────────────────────────────────────────────────────────────
    // Send — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends raw binary content to a direct target identified by <paramref name="target"/>.
    /// </summary>
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw binary content to receivers with the given <paramref name="name"/>.
    /// </summary>
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw binary content to receivers with the given <paramref name="type"/>.
    /// </summary>
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends raw binary content to a receiver by its unique <paramref name="id"/>.
    /// </summary>
    Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    // ──────────────────────────────────────────────────────────────────
    // Send — model
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a serialized model to receivers with the given <paramref name="name"/>.
    /// </summary>
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a serialized model to receivers with the given <paramref name="type"/>.
    /// </summary>
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a serialized model to a direct receiver by its unique <paramref name="id"/>.
    /// </summary>
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a serialized model to a direct receiver.
    /// The target is resolved from the <typeparamref name="T"/> attribute.
    /// </summary>
    Task<HorseResult> Send<T>(T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a serialized model to a specific <paramref name="target"/>.
    /// </summary>
    Task<HorseResult> SendAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    // ──────────────────────────────────────────────────────────────────
    // Request — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a raw binary request to a <paramref name="target"/> and waits for a raw response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw string request to a <paramref name="target"/> and waits for a raw response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an empty request (no content) to a <paramref name="target"/> and waits for a raw response.
    /// </summary>
    Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    // ──────────────────────────────────────────────────────────────────
    // Request — model
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a model request and waits for a typed <typeparamref name="TResponse"/> response.
    /// The target is resolved from the model's attribute.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a model request to a specific <paramref name="target"/> and waits for a typed <typeparamref name="TResponse"/> response.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TRequest, TResponse>(string target, TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a model request to a specific <paramref name="target"/> with an explicit content type and waits for a typed <typeparamref name="TResponse"/> response.
    /// </summary>
    Task<HorseResult<TResponse>> Request<TRequest, TResponse>(string target, ushort contentType, TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default);
}