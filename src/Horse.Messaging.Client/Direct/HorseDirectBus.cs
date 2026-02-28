using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct;

/// <summary>
/// Implementation for direct messages and requests
/// </summary>
public class HorseDirectBus<TIdentifier> : HorseDirectBus, IHorseDirectBus<TIdentifier>
{
    public HorseDirectBus(HorseClient client) : base(client) { }
}

/// <summary>
/// Implementation for direct messages and requests
/// </summary>
public class HorseDirectBus : IHorseDirectBus
{
    private readonly HorseClient _client;

    public HorseDirectBus(HorseClient client) => _client = client;

    /// <inheritdoc />
    public HorseClient GetClient() => _client;

    // ──────────────────────────────────────────────────────────────────
    // Send — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendAsync(target, contentType, content, waitForCommit, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendByName(name, contentType, content, waitForCommit, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendByType(type, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendAsync(id, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Send — model
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendByName(name, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendByType(type, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendById(id, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Send<T>(T model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Send(model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendAsync<T>(string target, ushort contentType, T model,
        bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.SendById(target, contentType, model, waitForAcknowledge, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Request — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request(target, contentType, content, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request(target, contentType, content, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request(target, contentType, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Request — model
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request<TResponse>(request, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TRequest, TResponse>(string target, TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request<TResponse>(target, null, request, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TRequest, TResponse>(string target, ushort contentType, TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null, CancellationToken cancellationToken = default)
        => _client.Direct.Request<TResponse>(target, contentType, request, messageHeaders, cancellationToken);
}