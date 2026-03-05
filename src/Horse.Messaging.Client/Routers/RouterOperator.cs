using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Horse.Messaging.Client.Routers;

/// <summary>
/// Router manager object for Horse client
/// </summary>
public class RouterOperator
{
    private readonly HorseClient _client;
    private readonly TypeDescriptorContainer<RouterTypeDescriptor> _descriptorContainer;

    internal RouterOperator(HorseClient client)
    {
        _client = client;
        _descriptorContainer = new TypeDescriptorContainer<RouterTypeDescriptor>(new RouterTypeResolver());
    }

    #region Actions

    /// <summary>
    /// Creates new router.
    /// Returns success result if router already exists.
    /// </summary>
    public async Task<HorseResult> Create(string name, RouteMethod method, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.CreateRouter;
        message.SetTarget(name);
        message.WaitResponse = true;
        message.AddHeader(HorseHeaders.ROUTE_METHOD, Convert.ToInt32(method).ToString());
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        return await _client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Gets information of all routers in server
    /// </summary>
    public async Task<HorseModelResult<List<RouterInformation>>> List(CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.ListRouters;
        return await _client.SendAsync<List<RouterInformation>>(message, cancellationToken);
    }

    /// <summary>
    /// Removes a router.
    /// Returns success result if router doesn't exists.
    /// </summary>
    public async Task<HorseResult> Remove(string name, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.RemoveRouter;
        message.SetTarget(name);
        message.WaitResponse = true;
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        return await _client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Adds new binding to a router.
    /// </summary>
    /// <param name="routerName">Router name of the binding.</param>
    /// <param name="type">Binding type.</param>
    /// <param name="name">Binding name.</param>
    /// <param name="target">Binding target. Queue name, tag name, direct receiver id, name, type, etc.</param>
    /// <param name="interaction">Binding interaction.</param>
    /// <param name="bindingMethod">Binding method is used when multiple receivers available in same binding.</param>
    /// <param name="contentType">Overwritten content type if specified.</param>
    /// <param name="priority">Binding priority.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> AddBinding(string routerName,
        string type,
        string name,
        string target,
        BindingInteraction interaction,
        RouteMethod bindingMethod,
        ushort? contentType,
        int priority,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.AddBinding;
        message.SetTarget(routerName);
        message.WaitResponse = true;
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        BindingInformation info = new BindingInformation
        {
            Name = name,
            Target = target,
            Interaction = interaction,
            ContentType = contentType,
            Priority = priority,
            BindingType = type,
            Method = bindingMethod
        };
        message.Serialize(info, _client.MessageSerializer);
        return await _client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Gets all bindings of a router
    /// </summary>
    public async Task<HorseModelResult<List<BindingInformation>>> GetBindings(string routerName, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.ListBindings;
        message.SetTarget(routerName);
        return await _client.SendAsync<List<BindingInformation>>(message, cancellationToken);
    }

    /// <summary>
    /// Remove a binding from a router
    /// </summary>
    public async Task<HorseResult> RemoveBinding(string routerName, string bindingName, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.RemoveBinding;
        message.SetTarget(routerName);
        message.WaitResponse = true;
        message.SetMessageId(_client.UniqueIdGenerator.Create());
        message.AddHeader(HorseHeaders.BINDING_NAME, bindingName);
        return await _client.WaitResponse(message, true, cancellationToken);
    }

    #endregion

    #region Publish

    /// <summary>
    /// Publishes raw binary content to a router.
    /// </summary>
    public Task<HorseResult> Publish(string routerName, byte[] data, CancellationToken cancellationToken = default)
    {
        return Publish(routerName, data, null, false, 0, null, cancellationToken);
    }

    /// <summary>
    /// Publishes raw binary content to a router.
    /// </summary>
    public Task<HorseResult> Publish(string routerName, byte[] data, bool waitForAcknowledge, CancellationToken cancellationToken = default)
    {
        return Publish(routerName, data, null, waitForAcknowledge, 0, null, cancellationToken);
    }

    /// <summary>
    /// Publishes raw binary content to a router.
    /// </summary>
    public async Task<HorseResult> Publish(string routerName, byte[] data, string messageId, bool waitForAcknowledge,
        ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        HorseMessage msg = new HorseMessage(MessageType.Router, routerName, contentType);

        if (!string.IsNullOrEmpty(messageId))
            msg.SetMessageId(messageId);
        else
            msg.SetMessageId(_client.UniqueIdGenerator.Create());

        msg.WaitResponse = waitForAcknowledge;
        msg.Content = new MemoryStream(data);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                msg.AddHeader(pair.Key, pair.Value);

        return await _client.WaitResponse(msg, waitForAcknowledge, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to a router. Router name resolved from attribute.
    /// </summary>
    public Task<HorseResult> Publish<T>(T model, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(null, model, null, false, null, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to a router. Router name resolved from attribute.
    /// </summary>
    public Task<HorseResult> Publish<T>(T model, bool waitForAcknowledge, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(null, model, null, waitForAcknowledge, null, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to the specified router.
    /// </summary>
    public Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(routerName, model, null, waitForAcknowledge, null, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to the specified router.
    /// </summary>
    public Task<HorseResult> Publish<T>(string routerName, T model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(routerName, model, null, waitForAcknowledge, null, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to the specified router with an explicit content type.
    /// </summary>
    public Task<HorseResult> Publish<T>(string routerName, T model, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(routerName, model, null, waitForAcknowledge, contentType, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Publishes a serialized model to the specified router with an explicit message id and content type.
    /// </summary>
    public Task<HorseResult> Publish<T>(string routerName, T model, string messageId, ushort? contentType, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return PublishObject(routerName, model, messageId, waitForAcknowledge, contentType, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Internal publish for object models
    /// </summary>
    internal async Task<HorseResult> PublishObject(string routerName, object model, string messageId,
        bool waitForAcknowledge, ushort? contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        RouterTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(model.GetType());

        if (!string.IsNullOrEmpty(routerName))
            descriptor.RouterName = routerName;

        if (contentType.HasValue)
            descriptor.ContentType = contentType.Value;

        HorseMessage message = descriptor.CreateMessage();

        if (!string.IsNullOrEmpty(messageId))
            message.SetMessageId(messageId);
        else
            message.SetMessageId(_client.UniqueIdGenerator.Create());

        message.WaitResponse = waitForAcknowledge;
        message.Serialize(model, _client.MessageSerializer);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await _client.WaitResponse(message, waitForAcknowledge, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request to a router and waits for a response.
    /// </summary>
    public Task<HorseMessage> PublishRequest(string routerName, string message, CancellationToken cancellationToken = default)
    {
        return PublishRequest(routerName, message, 0, null, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request to a router and waits for a response.
    /// </summary>
    public Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType, CancellationToken cancellationToken = default)
    {
        return PublishRequest(routerName, message, contentType, null, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string request to a router and waits for a response.
    /// </summary>
    public async Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        HorseMessage msg = new HorseMessage(MessageType.Router, routerName, contentType);
        msg.WaitResponse = true;
        msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                msg.AddHeader(pair.Key, pair.Value);

        return await _client.Request(msg, cancellationToken);
    }

    /// <summary>
    /// Publishes a model request and waits for a typed response. Router name from attribute.
    /// </summary>
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
        return PublishRequest<TRequest, TResponse>(null, request, null, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a model request and waits for a typed response. Router name from attribute.
    /// </summary>
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(TRequest request,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        return PublishRequest<TRequest, TResponse>(null, request, null, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Publishes a model request to the specified router and waits for a typed response.
    /// </summary>
    public Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        CancellationToken cancellationToken = default)
    {
        return PublishRequest<TRequest, TResponse>(routerName, request, null, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a model request to the specified router and waits for a typed response.
    /// </summary>
    public async Task<HorseResult<TResponse>> PublishRequest<TRequest, TResponse>(string routerName, TRequest request,
        ushort? contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        RouterTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(request.GetType());

        if (!string.IsNullOrEmpty(routerName))
            descriptor.RouterName = routerName;

        if (contentType.HasValue)
            descriptor.ContentType = contentType.Value;

        HorseMessage message = descriptor.CreateMessage();
        message.WaitResponse = true;
        message.Serialize(request, _client.MessageSerializer);

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

    #endregion
}

