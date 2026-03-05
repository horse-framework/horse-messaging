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
    /// <inheritdoc />
    public HorseDirectBus(HorseClient client) : base(client) { }
}

/// <summary>
/// Implementation for direct messages and requests
/// </summary>
public class HorseDirectBus : IHorseDirectBus
{
    private readonly HorseClient _client;

    /// <summary>
    /// Creates a new HorseDirectBus instance.
    /// </summary>
    /// <param name="client">The Horse client instance.</param>
    public HorseDirectBus(HorseClient client) => _client = client;

    /// <inheritdoc />
    public HorseClient GetClient() => _client;

    // ──────────────────────────────────────────────────────────────────
    // Send — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendAsync(target, contentType, content, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendAsync(target, contentType, content, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendByName(name, contentType, content, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendByName(name, contentType, content, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendByType(type, contentType, content, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendByType(type, contentType, content, waitForAcknowledge, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Send — model
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendByName(name, contentType, model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendByName(name, contentType, model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendByType(type, contentType, model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendByType(type, contentType, model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.SendById(id, contentType, model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.SendById(id, contentType, model, waitForAcknowledge, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Send<T>(T model, CancellationToken cancellationToken)
        => _client.Direct.Send(model, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Send<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken)
        => _client.Direct.Send(model, waitForAcknowledge, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Send<T>(T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Send(model, waitForAcknowledge, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Request — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, string content, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, content, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, content, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, content, cancellationToken);

    /// <inheritdoc />
    public Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Request(target, contentType, content, messageHeaders, cancellationToken);

    // ──────────────────────────────────────────────────────────────────
    // Request — model
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TResponse>(object request, CancellationToken cancellationToken)
        => _client.Direct.Request<TResponse>(request, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Request<TResponse>(request, messageHeaders, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request, CancellationToken cancellationToken)
        => _client.Direct.Request<TResponse>(target, contentType, request, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Direct.Request<TResponse>(target, contentType, request, messageHeaders, cancellationToken);
}