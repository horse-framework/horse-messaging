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
    // ── Send — raw content ──

    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    // ── Send — model ──

    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult> Send<T>(T model, bool waitAcknowledge, CancellationToken cancellationToken);
    Task<HorseResult> Send<T>(T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    // ── Request — raw content ──

    Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken);
    Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseMessage> Request(string target, ushort contentType, string content, CancellationToken cancellationToken);
    Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, CancellationToken cancellationToken);
    Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    // ── Request — model ──

    Task<HorseResult<TResponse>> Request<TResponse>(object request, CancellationToken cancellationToken);
    Task<HorseResult<TResponse>> Request<TResponse>(object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request, CancellationToken cancellationToken);
    Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
}