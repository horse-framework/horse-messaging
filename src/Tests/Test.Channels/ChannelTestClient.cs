using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;

namespace Test.Channels;

/// <summary>
/// Connected test client that records the channel messages it receives
/// </summary>
internal class ChannelTestClient
{
    public HorseClient Client { get; }
    public ConcurrentQueue<HorseMessage> Received { get; } = new();

    private ChannelTestClient(HorseClient client)
    {
        Client = client;
        client.MessageReceived += (_, m) =>
        {
            if (m.Type == MessageType.Channel)
                Received.Enqueue(m);
        };
    }

    public static async Task<ChannelTestClient> Connect(int port)
    {
        //short timeout: a lost response fails the test in seconds instead of 30 seconds per request
        HorseClient client = new HorseClient { ResponseTimeout = TimeSpan.FromSeconds(5) };
        ChannelTestClient testClient = new ChannelTestClient(client);
        await client.ConnectAsync($"horse://localhost:{port}");
        await TestHorseRider.WaitUntil(() => client.IsConnected && !string.IsNullOrEmpty(client.ClientId));

        if (!client.IsConnected || string.IsNullOrEmpty(client.ClientId))
            throw new InvalidOperationException($"Test client could not connect to port {port} (connected: {client.IsConnected}, id: {client.ClientId ?? "null"})");

        return testClient;
    }

    public string[] Contents => Received.Select(x => x.GetStringContent()).ToArray();

    public Task WaitForCount(int count, int timeoutMs = 3000)
    {
        return TestHorseRider.WaitUntil(() => Received.Count >= count, timeoutMs);
    }
}
