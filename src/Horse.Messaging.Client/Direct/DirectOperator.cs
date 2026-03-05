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
    /// Sends raw binary content to a direct target.
    /// </summary>
    public Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendAsync(target, contentType, content, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to a direct target.
    /// </summary>
    public async Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
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
    /// Sends a serialized model to receivers with the given name.
    /// </summary>
    public Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendById<T>("@name:" + name, contentType, model, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to receivers with the given name.
    /// </summary>
    public Task<HorseResult> SendByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return SendById<T>("@name:" + name, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to receivers with the given type.
    /// </summary>
    public Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendById<T>("@type:" + type, contentType, model, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to receivers with the given type.
    /// </summary>
    public Task<HorseResult> SendByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return SendById<T>("@type:" + type, contentType, model, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver by its unique id.
    /// </summary>
    public Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendById(id, contentType, model, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver by its unique id.
    /// </summary>
    public async Task<HorseResult> SendById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
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
    /// Sends a serialized model to a direct receiver resolved from the model's attribute without waiting for acknowledgement.
    /// </summary>
    /// <param name="model">The message model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Send<T>(T model, CancellationToken cancellationToken)
    {
        return Send(model, false, null, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model.</param>
    /// <param name="waitAcknowledge">If true, waits for the target to acknowledge receipt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Send<T>(T model, bool waitAcknowledge, CancellationToken cancellationToken)
    {
        return Send(model, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends a serialized model to a direct receiver resolved from the model's attribute.
    /// </summary>
    public async Task<HorseResult> Send<T>(T model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
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
    /// Sends raw binary content to receivers with the given name.
    /// </summary>
    public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendById("@name:" + name, contentType, content, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to receivers with the given name.
    /// </summary>
    public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return SendById("@name:" + name, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to receivers with the given type.
    /// </summary>
    public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        CancellationToken cancellationToken)
    {
        return SendById("@type:" + type, contentType, content, waitAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Sends raw binary content to receivers with the given type.
    /// </summary>
    public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return SendById("@type:" + type, contentType, content, waitAcknowledge, messageHeaders, cancellationToken);
    }

    private async Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
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
    /// Sends a model request and waits for a typed response. Target resolved from attribute.
    /// </summary>
    public Task<HorseResult<TResponse>> Request<TResponse>(object model, CancellationToken cancellationToken)
    {
        return Request<TResponse>(null, null, model, null, cancellationToken);
    }

    /// <summary>
    /// Sends a model request and waits for a typed response. Target resolved from attribute.
    /// </summary>
    public Task<HorseResult<TResponse>> Request<TResponse>(object model,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return Request<TResponse>(null, null, model, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Sends a model request to a specific target and waits for a typed response.
    /// </summary>
    public Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object model,
        CancellationToken cancellationToken)
    {
        return Request<TResponse>(target, contentType, model, null, cancellationToken);
    }

    /// <summary>
    /// Sends a model request to a specific target and waits for a typed response.
    /// </summary>
    public async Task<HorseResult<TResponse>> Request<TResponse>(string target, ushort? contentType, object model,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
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
    /// Sends a raw binary request and waits for a raw response.
    /// </summary>
    public Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        CancellationToken cancellationToken)
    {
        return Request(target, contentType, content, null, cancellationToken);
    }

    /// <summary>
    /// Sends a raw binary request and waits for a raw response.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
        message.Content = content;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request and waits for a raw response.
    /// </summary>
    public Task<HorseMessage> Request(string target, ushort contentType, string content,
        CancellationToken cancellationToken)
    {
        return Request(target, contentType, content, null, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request and waits for a raw response.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType, string content,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
        message.SetStringContent(content);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    /// <summary>
    /// Sends an empty request (no content) and waits for a raw response.
    /// </summary>
    public Task<HorseMessage> Request(string target, ushort contentType, CancellationToken cancellationToken)
    {
        return Request(target, contentType, (IEnumerable<KeyValuePair<string, string>>)null, cancellationToken);
    }

    /// <summary>
    /// Sends an empty request (no content) and waits for a raw response.
    /// </summary>
    public async Task<HorseMessage> Request(string target, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.Request(message, cancellationToken);
    }

    #endregion
}

