using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels;

/// <summary>
/// Channel client object
/// </summary>
public class ChannelClient
{
    /// <summary>
    /// Channel
    /// </summary>
    public HorseChannel Channel { get; }

    /// <summary>
    /// Client
    /// </summary>
    public MessagingClient Client { get; }

    /// <summary>
    /// Subscription date
    /// </summary>
    public DateTime SubscribedAt { get; }

    private readonly Channel<byte[]> _sendQueue = System.Threading.Channels.Channel
        .CreateBounded<byte[]>(
            new BoundedChannelOptions(100)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });

    /// <summary>
    /// Creates new channel client object
    /// </summary>
    public ChannelClient(HorseChannel channel, MessagingClient client)
    {
        Channel = channel;
        Client = client;
        SubscribedAt = DateTime.UtcNow;
    }

    internal void SendChannelMessage(byte[] message)
    {
        _sendQueue.Writer.TryWrite(message);
    }

    internal async Task Run()
    {
        await foreach (byte[] data in _sendQueue.Reader.ReadAllAsync())
            await Client.SendAsync(data);
    }

    internal void Dispose()
    {
        try
        {
            _sendQueue.Writer.Complete();
        }
        catch
        {
        }
    }
}