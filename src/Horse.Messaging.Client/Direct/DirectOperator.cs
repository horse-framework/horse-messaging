using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct;

/// <summary>
/// Operator for sending direct (peer-to-peer) messages and request/response calls via a Horse client.
/// </summary>
public class DirectOperator
{
    private readonly HorseClient _client;
    private readonly TypeDescriptorContainer<DirectTypeDescriptor> _descriptorContainer;

    internal List<DirectHandlerRegistration> Registrations { get; } = new();

    internal DirectOperator(HorseClient client)
    {
        _client = client;
        _descriptorContainer = new TypeDescriptorContainer<DirectTypeDescriptor>(new DirectTypeResolver());
    }

    internal async Task OnDirectMessage(HorseMessage message)
    {
        DirectHandlerRegistration reg = Registrations.FirstOrDefault(x => x.ContentType == message.ContentType);
        if (reg == null)
            return;

        object model = reg.MessageType == typeof(string)
            ? message.GetStringContent()
            : _client.MessageSerializer.Deserialize(message, reg.MessageType);

        try
        {
            await reg.ConsumerExecuter.Execute(_client, message, model, _client.ConsumeToken);
        }
        catch (Exception ex)
        {
            _client.OnException(ex, message);
        }
    }

    #region Send

    /// <summary>
    /// Sends raw binary content to a direct target identified by <paramref name="target"/>.
    /// </summary>
    public async Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.SetTarget(target);
        message.ContentType = contentType;
        message.Content = content;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (waitAcknowledge)
            return await _client.SendAsync(message, true, cancellationToken);

        return await _client.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to receivers with the given <paramref name="name"/>.
    /// </summary>
    public async Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return await SendById<T>("@name:" + name, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to receivers with the given <paramref name="type"/>.
    /// </summary>
    public async Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return await SendById<T>("@type:" + type, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver by its unique <paramref name="id"/>.
    /// </summary>
    public async Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.SetTarget(id);
        message.Type = MessageType.DirectMessage;
        message.ContentType = contentType;
        message.Serialize(model, _client.MessageSerializer);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (waitAcknowledge)
            return await _client.SendAsync(message, true, cancellationToken);

        return await _client.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver.
    /// The target is resolved from the <typeparamref name="T"/> attribute.
    /// </summary>
    public async Task<HorseResult> Send<T>(T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        DirectTypeDescriptor descriptor = _descriptorContainer.GetDescriptor<T>();
        HorseMessage message = descriptor.CreateMessage();
        if (string.IsNullOrEmpty(message.Target))
            return new HorseResult(HorseResultCode.SendError);

        message.WaitResponse = waitAcknowledge;
        message.Serialize(model, _client.MessageSerializer);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (waitAcknowledge)
            return await _client.SendAsync(message, true, cancellationToken);

        return await _client.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to receivers with the given <paramref name="name"/>.
    /// </summary>
    public async Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return await SendById("@name:" + name, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to receivers with the given <paramref name="type"/>.
    /// </summary>
    public async Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return await SendById("@type:" + type, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);
    }

    private async Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.SetTarget(id);
        message.ContentType = contentType;
        message.Content = content;
        message.Type = MessageType.DirectMessage;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (waitAcknowledge)
            return await _client.SendAsync(message, true, cancellationToken);

        return await _client.SendAsync(message, cancellationToken);
    }

    #endregion

    #region Request

    /// <summary>
    /// Sends a model request and waits for a typed <typeparamref name="TResponse"/> response.
    /// The target is resolved from the model's attribute.
    /// </summary>
    public Task<HorseResult<TResponse>> Request<TResponse>(object model,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return Request<TResponse>(null, null, model, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a model request to a specific <paramref name="target"/> and waits for a typed <typeparamref name="TResponse"/> response.
    /// </summary>
    public async Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object model,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        DirectTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(model.GetType());

        HorseMessage message = descriptor != null
            ? descriptor.CreateMessage(target)
            : new HorseMessage(MessageType.DirectMessage, target, contentType ?? 0);

        if (contentType.HasValue)
            message.ContentType = contentType.Value;

        message.Serialize(model, _client.MessageSerializer);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        HorseMessage responseMessage = await _client.Request(message, cancellationToken);
        if (responseMessage.ContentType == 0)
        {
            TResponse response = responseMessage.Deserialize<TResponse>(_client.MessageSerializer);
            return new HorseResult<TResponse>(response, message, HorseResultCode.Ok);
        }

        return new HorseResult<TResponse>(default, responseMessage, (HorseResultCode)responseMessage.ContentType);
    }

    /// <summary>
    /// Sends a raw binary request to a <paramref name="target"/> and waits for a raw response.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
        message.Content = content;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request to a <paramref name="target"/> and waits for a raw response.
    /// The <paramref name="contentType"/> parameter disambiguates this from model-based overloads.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
        message.SetStringContent(content);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    /// <summary>
    /// Sends an empty request (no content) to a <paramref name="target"/> and waits for a raw response.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    #endregion
}