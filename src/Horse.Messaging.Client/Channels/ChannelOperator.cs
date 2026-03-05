using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Handler for queue name generator
/// </summary>
public delegate string ChannelNameHandler(ChannelNameHandlerContext context);

/// <summary>
/// Channel operator
/// </summary>
public class ChannelOperator
{
    private readonly TypeDescriptorContainer<ChannelTypeDescriptor> _descriptorContainer;
    private int _activeChannelOperations;

    internal HorseClient Client { get; }
    internal List<ChannelSubscriberRegistration> Registrations { get; } = new();
    
    /// <summary>
    /// Returns count of consume operations
    /// </summary>
    public int ActiveChannelOperations => _activeChannelOperations;

    /// <summary>
    /// Channel name handler
    /// </summary>
    public ChannelNameHandler NameHandler { get; set; }

    internal ChannelOperator(HorseClient client)
    {
        Client = client;
        _descriptorContainer = new TypeDescriptorContainer<ChannelTypeDescriptor>(new ChannelTypeResolver(Client));
    }

    internal async Task OnChannelMessage(HorseMessage message)
    {
        ChannelSubscriberRegistration reg = Registrations.FirstOrDefault(x => x.Name == message.Target);
        if (reg == null)
            return;

        object model = reg.MessageType == typeof(string)
            ? message.GetStringContent()
            : Client.MessageSerializer.Deserialize(message, reg.MessageType);

        try
        {
            if (reg.Filter != null && !reg.Filter(message, model))
                return;

            Interlocked.Increment(ref _activeChannelOperations);
            await reg.Executer.Execute(Client, message, model, Client.ConsumeToken);
        }
        catch (Exception ex)
        {
            Client.OnException(ex, message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeChannelOperations);
        }
    }

    /// <summary>
    /// Creates a new channel with default options.
    /// </summary>
    /// <param name="channel">Channel name to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string channel, CancellationToken cancellationToken)
    {
        return Create(channel, null, false, cancellationToken);
    }

    /// <summary>
    /// Creates a new channel.
    /// </summary>
    /// <param name="channel">Channel name to create.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string channel, bool verifyResponse, CancellationToken cancellationToken)
    {
        return Create(channel, null, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Creates a new channel with custom options.
    /// </summary>
    /// <param name="channel">Channel name to create.</param>
    /// <param name="options">Action to configure channel options.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelCreate;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (options != null)
        {
            ChannelOptions o = new ChannelOptions();
            options(o);

            if (o.AutoDestroy.HasValue)
                message.AddHeader(HorseHeaders.AUTO_DESTROY, o.AutoDestroy.Value.ToString());

            if (o.ClientLimit.HasValue)
                message.AddHeader(HorseHeaders.CLIENT_LIMIT, o.ClientLimit.Value.ToString());

            if (o.MessageSizeLimit.HasValue)
                message.AddHeader(HorseHeaders.MESSAGE_SIZE_LIMIT, o.MessageSizeLimit.Value.ToString());

            if (o.SendLastMessageAsInitial.HasValue)
                message.AddHeader(HorseHeaders.CHANNEL_INITIAL_MESSAGE, o.SendLastMessageAsInitial.Value ? "1" : "0");

            if (o.AutoDestroyIdleSeconds.HasValue)
                message.AddHeader(HorseHeaders.CHANNEL_DESTROY_IDLE_SECONDS, o.AutoDestroyIdleSeconds.Value.ToString());
        }

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    /// <param name="channel">Channel name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Delete(string channel, CancellationToken cancellationToken)
    {
        return Delete(channel, false, cancellationToken);
    }

    /// <summary>
    /// Deletes a channel.
    /// </summary>
    /// <param name="channel">Channel name to delete.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Delete(string channel, bool verifyResponse, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelRemove;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a channel.
    /// </summary>
    /// <param name="channel">Channel name to subscribe to.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe(string channel, bool verifyResponse, CancellationToken cancellationToken)
    {
        return Subscribe(channel, verifyResponse, null, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a channel.
    /// </summary>
    /// <param name="channel">Channel name to subscribe to.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="headers">Additional headers to include in the subscription request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Subscribe(string channel, bool verifyResponse,
        IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelSubscribe;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (headers != null)
            foreach (KeyValuePair<string, string> header in headers)
                message.AddHeader(header.Key, header.Value);

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from a channel.
    /// </summary>
    /// <param name="channel">Channel name to unsubscribe from.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the unsubscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Unsubscribe(string channel, bool verifyResponse, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelUnsubscribe;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Finds in all channels in server
    /// </summary>
    public Task<HorseModelResult<List<ChannelInformation>>> List(CancellationToken cancellationToken)
    {
        return List(null, cancellationToken);
    }

    /// <summary>
    /// Finds in all channels in server
    /// </summary>
    public async Task<HorseModelResult<List<ChannelInformation>>> List(string filter, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.ContentType = KnownContentTypes.ChannelList;

        if (!string.IsNullOrEmpty(filter))
            message.AddHeader(HorseHeaders.FILTER, filter);

        return await Client.SendAsync<List<ChannelInformation>>(message, cancellationToken);
    }

    /// <summary>
    /// Gets all subscribers of a channel.
    /// </summary>
    /// <param name="channelName">Channel name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseModelResult<List<ClientInformation>>> GetSubscribers(string channelName, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.SetTarget(channelName);
        message.ContentType = KnownContentTypes.ChannelSubscribers;
        message.SetMessageId(Client.UniqueIdGenerator.Create());

        message.AddHeader(HorseHeaders.CHANNEL_NAME, channelName);

        return await Client.SendAsync<List<ClientInformation>>(message, cancellationToken);
    }

    #region Publish

    /// <summary>
    /// Publishes a model message to a channel. Channel name is resolved from the model's attribute.
    /// </summary>
    /// <param name="jsonObject">The model object to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Publish(object jsonObject, CancellationToken cancellationToken)
    {
        return Publish(null, jsonObject, false, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a model message to a channel. Channel name is resolved from the model's attribute.
    /// </summary>
    /// <param name="jsonObject">The model object to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Publish(object jsonObject, bool waitForAcknowledge, CancellationToken cancellationToken)
    {
        return Publish(null, jsonObject, waitForAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a model message to a specified channel.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="jsonObject">The model object to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Publish(string channel, object jsonObject, bool waitForAcknowledge, CancellationToken cancellationToken)
    {
        return Publish(channel, jsonObject, waitForAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a model message to a specified channel with custom headers.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="jsonObject">The model object to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Publish(string channel, object jsonObject, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        ChannelTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(jsonObject.GetType());

        if (!string.IsNullOrEmpty(channel))
            descriptor.Name = channel;

        HorseMessage message = descriptor.CreateMessage();
        message.WaitResponse = waitForAcknowledge;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        message.Serialize(jsonObject, Client.MessageSerializer);

        if (string.IsNullOrEmpty(message.MessageId) && waitForAcknowledge)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, waitForAcknowledge, cancellationToken);
    }

    /// <summary>
    /// Publishes a string message to a channel.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">String content to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishString(string channel, string content, CancellationToken cancellationToken)
    {
        return PublishData(channel, new MemoryStream(Encoding.UTF8.GetBytes(content)), false, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a string message to a channel.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">String content to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishString(string channel, string content, bool waitForAcknowledge, CancellationToken cancellationToken)
    {
        return PublishData(channel, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitForAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Publishes a string message to a channel with custom headers.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">String content to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishString(string channel, string content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        return PublishData(channel, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitForAcknowledge, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Publishes binary data to a channel.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">Binary content to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishData(string channel, MemoryStream content, CancellationToken cancellationToken)
    {
        return PublishData(channel, content, false, null, cancellationToken);
    }

    /// <summary>
    /// Publishes binary data to a channel.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">Binary content to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken)
    {
        return PublishData(channel, content, waitForAcknowledge, null, cancellationToken);
    }

    /// <summary>
    /// Publishes binary data to a channel with custom headers.
    /// </summary>
    /// <param name="channel">Target channel name.</param>
    /// <param name="content">Binary content to publish.</param>
    /// <param name="waitForAcknowledge">If true, waits for server acknowledgement.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage(MessageType.Channel, channel, KnownContentTypes.ChannelPush);
        message.Content = content;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return Client.WaitResponse(message, waitForAcknowledge, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Unsubscribes from all channels on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> UnsubscribeFromAllChannels(CancellationToken cancellationToken)
    {
        return Unsubscribe("*", true, cancellationToken);
    }
}