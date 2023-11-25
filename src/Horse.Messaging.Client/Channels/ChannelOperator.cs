using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

    internal HorseClient Client { get; }
    internal List<ChannelSubscriberRegistration> Registrations { get; } = new();

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
            await reg.Executer.Execute(Client, message, model);
        }
        catch (Exception ex)
        {
            Client.OnException(ex, message);
        }
    }

    /// <summary>
    /// Creates new channel
    /// </summary>
    public Task<HorseResult> Create(string channel, Action<ChannelOptions> options = null, bool verifyResponse = false)
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

        return Client.WaitResponse(message, verifyResponse);
    }

    /// <summary>
    /// Creates a channel
    /// </summary>
    public Task<HorseResult> Delete(string channel, bool verifyResponse = false)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelRemove;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse);
    }

    /// <summary>
    /// Subscribes to a channel
    /// </summary>
    public async Task<HorseResult> Subscribe(string channel, bool verifyResponse, IEnumerable<KeyValuePair<string, string>> headers = null)
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

        return await Client.WaitResponse(message, verifyResponse);
    }

    /// <summary>
    /// Unsubscribes from a channel
    /// </summary>
    public async Task<HorseResult> Unsubscribe(string channel, bool verifyResponse)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.ContentType = KnownContentTypes.ChannelUnsubscribe;
        message.SetTarget(channel);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, verifyResponse);
    }

    /// <summary>
    /// Finds in all channels in server
    /// </summary>
    public async Task<HorseModelResult<List<ChannelInformation>>> List(string filter = null)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.ContentType = KnownContentTypes.ChannelList;
        message.AddHeader(HorseHeaders.FILTER, filter);
        return await Client.SendAndGetJson<List<ChannelInformation>>(message);
    }

    /// <summary>
    /// Gets all subscribers of channel
    /// </summary>
    public async Task<HorseModelResult<List<ClientInformation>>> GetSubscribers(string channelName)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Channel;
        message.SetTarget(channelName);
        message.ContentType = KnownContentTypes.ChannelSubscribers;
        message.SetMessageId(Client.UniqueIdGenerator.Create());

        message.AddHeader(HorseHeaders.CHANNEL_NAME, channelName);

        return await Client.SendAndGetJson<List<ClientInformation>>(message);
    }

    #region Publish

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    public Task<HorseResult> Publish(object jsonObject, bool waitAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
    {
        return Publish(null, jsonObject, waitAcknowledge, messageHeaders);
    }

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    public async Task<HorseResult> Publish(string channel, object jsonObject, bool waitAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
    {
        ChannelTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(jsonObject.GetType());

        if (!string.IsNullOrEmpty(channel))
            descriptor.Name = channel;

        HorseMessage message = descriptor.CreateMessage();
        message.WaitResponse = waitAcknowledge;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        message.Serialize(jsonObject, Client.MessageSerializer);

        if (string.IsNullOrEmpty(message.MessageId) && waitAcknowledge)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, waitAcknowledge);
    }

    /// <summary>
    /// Pushes a message to a queue
    /// </summary>
    public async Task<HorseResult> PublishString(string channel, string content, bool waitAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
    {
        return await PublishData(channel, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitAcknowledge, messageHeaders);
    }

    /// <summary>
    /// Pushes a message to a queue
    /// </summary>
    public async Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
    {
        HorseMessage message = new HorseMessage(MessageType.Channel, channel, KnownContentTypes.ChannelPush);
        message.Content = content;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        return await Client.WaitResponse(message, waitAcknowledge);
    }

    #endregion
}