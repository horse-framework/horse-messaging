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
        {
            try
            {
                if (Client.SwitchingProtocol != null)
                    await Client.SwitchingProtocol.SendAsync(data);
                else
                    await Client.SendAsync(data);
            }
            catch
            {
                //a failed send skips only that message. Without catching, the loop ended and the client
                //silently stopped receiving while it was still listed as a subscriber.
                //Not logged, a dropped connection would log once per message.
            }

            //disconnect is final for a messaging client, its subscriptions are removed by the client rider
            if (!Client.IsConnected)
                break;
        }
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