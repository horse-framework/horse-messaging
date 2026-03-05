using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

internal class HorseChannelBus<TIdentifier> : HorseChannelBus, IHorseChannelBus<TIdentifier>
{
    public HorseChannelBus(HorseClient client) : base(client)
    {
    }
}

internal class HorseChannelBus : IHorseChannelBus
{
    private readonly HorseClient _client;

    /// <summary>
    /// Creates new horse channel bus
    /// </summary>
    public HorseChannelBus(HorseClient client)
    {
        _client = client;
    }

    public HorseClient GetClient()
    {
        return _client;
    }

    public Task<HorseResult> Create(string channel, CancellationToken cancellationToken)
        => _client.Channel.Create(channel, cancellationToken);

    public Task<HorseResult> Create(string channel, bool verifyResponse, CancellationToken cancellationToken)
        => _client.Channel.Create(channel, verifyResponse, cancellationToken);

    public Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse, CancellationToken cancellationToken)
        => _client.Channel.Create(channel, options, verifyResponse, cancellationToken);

    public Task<HorseResult> Delete(string channel, CancellationToken cancellationToken)
        => _client.Channel.Delete(channel, cancellationToken);

    public Task<HorseResult> Delete(string channel, bool verifyResponse, CancellationToken cancellationToken)
        => _client.Channel.Delete(channel, verifyResponse, cancellationToken);

    public Task<HorseResult> Publish(object model, CancellationToken cancellationToken)
        => _client.Channel.Publish(model, cancellationToken);

    public Task<HorseResult> Publish(object model, bool waitAcknowledge, CancellationToken cancellationToken)
        => _client.Channel.Publish(model, waitAcknowledge, cancellationToken);

    public Task<HorseResult> Publish(string channel, object model, bool waitAcknowledge, CancellationToken cancellationToken)
        => _client.Channel.Publish(channel, model, waitAcknowledge, cancellationToken);

    public Task<HorseResult> Publish(string channel, object model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Channel.Publish(channel, model, waitAcknowledge, messageHeaders, cancellationToken);

    public Task<HorseResult> PublishString(string channel, string message, CancellationToken cancellationToken)
        => _client.Channel.PublishString(channel, message, cancellationToken);

    public Task<HorseResult> PublishString(string channel, string message, bool waitAcknowledge, CancellationToken cancellationToken)
        => _client.Channel.PublishString(channel, message, waitAcknowledge, cancellationToken);

    public Task<HorseResult> PublishString(string channel, string message, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Channel.PublishString(channel, message, waitAcknowledge, messageHeaders, cancellationToken);

    public Task<HorseResult> PublishData(string channel, MemoryStream content, CancellationToken cancellationToken)
        => _client.Channel.PublishData(channel, content, cancellationToken);

    public Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken)
        => _client.Channel.PublishData(channel, content, waitAcknowledge, cancellationToken);

    public Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => _client.Channel.PublishData(channel, content, waitAcknowledge, messageHeaders, cancellationToken);
}