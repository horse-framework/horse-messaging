using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Events;

/// <summary>
/// Outgoing event messages of a client.
/// Event managers only enqueue serialized events, a single loop per client sends them in order.
/// A slow client fills only its own buffer. When the buffer is full, the oldest event is dropped.
/// </summary>
internal sealed class ClientEventOutbox
{
    private readonly MessagingClient _client;
    private readonly Channel<byte[]> _channel;

    internal ClientEventOutbox(MessagingClient client, int capacity)
    {
        _client = client;

        if (capacity > 0)
        {
            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            }, _ => client.Stats.IncrementDroppedEvents());
        }
        else
        {
            _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        }
    }

    internal void Start()
    {
        _ = Task.Run(SendLoop);
    }

    internal void Enqueue(byte[] data)
    {
        // returns false only after completion (client disconnected), the event is dropped then
        _channel.Writer.TryWrite(data);
    }

    /// <summary>
    /// Stops the outbox when the client disconnects. Only completes the channel, it does not read:
    /// the send loop is the single reader (SingleReader = true) and it drops the buffered events when it ends.
    /// </summary>
    internal void Complete()
    {
        _channel.Writer.TryComplete();
    }

    private async Task SendLoop()
    {
        ChannelReader<byte[]> reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out byte[] data))
                {
                    if (!_client.IsConnected)
                        return;

                    try
                    {
                        await _client.SendRawAsync(data);
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex);
        }
        finally
        {
            // Read here, by the single reader: releases the buffered events of the disconnected client.
            // Completed first, so no event is written after the loop ends.
            _channel.Writer.TryComplete();
            while (reader.TryRead(out _))
            {
            }
        }
    }

    private void Log(Exception ex)
    {
        try
        {
            _client.HorseRider.SendError(HorseLogLevel.Error, HorseLogEvents.EventTrigger, $"Event Send Client:{_client.UniqueId}", ex);
        }
        catch
        {
            // logging must not stop the send loop
        }
    }
}
