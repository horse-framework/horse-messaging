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

    public Task<HorseResult> Create(string channel, Action<ChannelOptions> options = null, bool verifyResponse = false,
        CancellationToken cancellationToken = default)
        => _client.Channel.Create(channel, options, verifyResponse, cancellationToken);

    public Task<HorseResult> Delete(string channel, bool verifyResponse = false,
        CancellationToken cancellationToken = default)
        => _client.Channel.Delete(channel, verifyResponse, cancellationToken);

    public Task<HorseResult> Publish(object model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
        => _client.Channel.Publish(model, waitForAcknowledge, messageHeaders, cancellationToken);

    public Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
        => _client.Channel.Publish(channel, model, waitForAcknowledge, messageHeaders, cancellationToken);

    public Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
        => _client.Channel.PublishString(channel, message, waitForAcknowledge, messageHeaders, cancellationToken);

    public Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        CancellationToken cancellationToken = default)
        => _client.Channel.PublishData(channel, content, waitForAcknowledge, messageHeaders, cancellationToken);
}