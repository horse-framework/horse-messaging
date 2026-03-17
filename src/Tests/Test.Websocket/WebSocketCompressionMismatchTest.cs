using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.OverWebSockets;
using Xunit;

namespace Test.Websocket;

/// <summary>
/// Dummy channel model and subscriber for testing AutoSubscribe with channel registration.
/// </summary>
[ChannelName("Tickers")]
public class TickerMessage
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }
}

public class TestTickerSubscriber : IChannelSubscriber<TickerMessage>
{
    public Task Handle(TickerMessage model, HorseMessage rawMessage, HorseClient client, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task Error(Exception exception, TickerMessage model, HorseMessage rawMessage, HorseClient client, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// Reproduces the missing HELLO message bug for WebSocket connections.
///
/// HorseSocket.cs:155-167 — When SwitchingProtocol is active (WebSocket mode),
/// only ClientProtocolHandshake() is called (HTTP Upgrade). SendInfoMessage()
/// is SKIPPED entirely. This means:
///
///   1. CLIENT_ID is never sent to the server
///   2. Server never sends Accepted response → _clientId stays null
///   3. Every ClientId access generates a NEW random ID
///   4. AutoSubscribe fires immediately → Channel.Subscribe(verifyResponse: true)
///   5. Server can't match response to the right client → subscribe fails
///   6. DisconnectionOnAutoJoinFailure → disconnect
///
/// Normal Horse protocol calls SendInfoMessage() right after protocol handshake,
/// which sends the HELLO message with CLIENT_ID. Server responds with Accepted.
/// </summary>
public class WebSocketMissingHelloTest
{
    /// <summary>
    /// Verifies that a WebSocket-connected client receives a proper ClientId
    /// from the server (via Accepted message).
    ///
    /// Normal Horse protocol: HELLO → server assigns ID → Accepted → ClientId set.
    /// WebSocket: HELLO is skipped → no Accepted → ClientId is random on every access.
    ///
    /// EXPECTED: ClientId should be stable (same value on multiple accesses).
    /// ACTUAL (BUG): ClientId returns a different random value each time because
    /// _clientId is never set (no Accepted message received).
    /// </summary>
    [Fact]
    public async Task WebSocket_ClientId_ShouldBeStable_AfterConnect()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-clientid-test")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(2000);

            Assert.True(client.IsConnected, "Client should be connected.");

            // Read ClientId twice — it should return the SAME value
            string id1 = client.ClientId;
            string id2 = client.ClientId;

            // BUG: Without Accepted message, _clientId is null.
            // Each ClientId access calls UniqueIdGenerator.Create() → different value each time.
            Assert.Equal(id1, id2);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Verifies that the server recognizes the WebSocket client after connect.
    ///
    /// Normal Horse: HELLO sent → server creates MessagingClient with CLIENT_ID.
    /// WebSocket: HELLO skipped → server may not have a properly identified client.
    ///
    /// EXPECTED: Server should know the client by its ClientId.
    /// ACTUAL (BUG): Server may not have a matching client because CLIENT_ID
    /// was never communicated via HELLO message.
    /// </summary>
    [Fact]
    public async Task WebSocket_ServerShouldKnowClient_AfterConnect()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-server-knows")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(2000);

            Assert.True(client.IsConnected);

            // Wait until ClientId stabilizes (Accepted message received)
            // Once _clientId is set by Accepted, two consecutive reads return the same value.
            string id1 = null, id2 = null;
            for (int i = 0; i < 30; i++)
            {
                id1 = client.ClientId;
                id2 = client.ClientId;
                if (id1 == id2) break;
                await Task.Delay(100);
            }

            Assert.Equal(id1, id2);

            // The server should have a MessagingClient matching this stable client ID
            var serverClient = server.Rider.Client.Find(id1);

            // Debug: list all known clients on the server
            if (serverClient == null)
            {
                var allClients = server.Rider.Client.Clients.ToList();
                string info = $"Client thinks its ID is '{id1}'. Server knows {allClients.Count} client(s): "
                              + string.Join(", ", allClients.Select(c => $"'{c.UniqueId}'"));
                Assert.Fail(info);
            }
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// WebSocket client manually subscribes to a channel with verifyResponse=true.
    /// Without HELLO, the server may not properly handle the subscription.
    ///
    /// EXPECTED: Subscribe returns Ok.
    /// ACTUAL (BUG): Subscribe fails (RequestTimeout or error) because the server
    /// cannot properly identify the client or route the response.
    /// </summary>
    [Fact]
    public async Task WebSocket_ManualChannelSubscribe_ShouldSucceed()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        await server.Rider.Channel.Create("TestChannel");

        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-manual-sub")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(1000);
            Assert.True(client.IsConnected, "Client should connect successfully.");

            HorseResult result = await client.Channel.Subscribe("TestChannel", true, CancellationToken.None);

            // BUG: Subscribe fails because HELLO was never sent.
            // Without proper client identification, server can't process the request.
            Assert.Equal(HorseResultCode.Ok, result.Code);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// WebSocket client with AutoSubscribe enabled and channel subscriber registered.
    /// This is the EXACT scenario from the bug report screenshot.
    ///
    /// AutoSubscribe fires immediately after connect, sends Channel.Subscribe
    /// with verifyResponse=true. Without HELLO, subscribe fails.
    /// DisconnectionOnAutoJoinFailure (default true) disconnects the client.
    ///
    /// We wait long enough (15s) for the subscribe to time out and trigger disconnect.
    ///
    /// EXPECTED: Client stays connected.
    /// ACTUAL (BUG): Client disconnects after subscribe timeout.
    /// </summary>
    [Fact]
    public async Task WebSocket_AutoSubscribeChannel_ShouldNotDisconnect()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        await server.Rider.Channel.Create("Tickers");

        var (_, wsPort) = server.Start(requestTimeout: 5);
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            bool disconnected = false;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-autosub-test")
                .SetClientType("test-type")
                .AddSingletonChannelSubscribers(typeof(TestTickerSubscriber))
                .UseHorseOverWebSockets()
                .Build();

            // AutoSubscribe = true is DEFAULT
            client.DisconnectionOnAutoJoinFailure = true;
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            // Wait long enough for AutoSubscribe's Channel.Subscribe to time out.
            // requestTimeout=5s, so we wait 10s to be safe.
            await Task.Delay(10_000);

            Assert.False(disconnected,
                "Client disconnected. Without HELLO message, channel subscribe fails " +
                "and DisconnectionOnAutoJoinFailure triggers disconnect.");
            Assert.True(client.IsConnected);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Control: Same scenario over normal Horse protocol — works perfectly.
    /// HELLO is sent → Accepted received → ClientId stable → subscribe succeeds.
    /// </summary>
    [Fact]
    public async Task HorseProtocol_AutoSubscribeChannel_Works()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        await server.Rider.Channel.Create("Tickers");

        var (horsePort, _) = server.Start(requestTimeout: 5);
        Assert.True(horsePort > 0, "Server failed to start");

        try
        {
            bool disconnected = false;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("horse-autosub-test")
                .SetClientType("test-type")
                .AddSingletonChannelSubscribers(typeof(TestTickerSubscriber))
                .Build();

            client.DisconnectionOnAutoJoinFailure = true;
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"horse://localhost:{horsePort}");
            await Task.Delay(3000);

            // Normal Horse protocol — HELLO sent, Accepted received, subscribe works
            Assert.False(disconnected, "Horse protocol client should not disconnect.");
            Assert.True(client.IsConnected);

            // ClientId should be stable
            string id1 = client.ClientId;
            string id2 = client.ClientId;
            Assert.Equal(id1, id2);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Control: WebSocket WITHOUT channel subscribers stays connected.
    /// No subscribe message → no failure → no disconnect.
    /// </summary>
    [Fact]
    public async Task WebSocket_NoSubscribers_StaysConnected()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-no-sub")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(2000);

            Assert.True(client.IsConnected);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Reproduces the exact "parrot" scenario from the screenshot:
    ///
    ///   1. Server creates a channel ("Tickers") with SendLastMessageAsInitial = true
    ///   2. A message is published to the channel BEFORE the client connects
    ///      (so there IS an initial message waiting)
    ///   3. Client connects over WebSocket, waits 1 second, then subscribes
    ///      to "Tickers" with verifyResponse=true
    ///   4. Server sends back the subscribe OK response AND the initial message
    ///      almost simultaneously
    ///
    /// BUG: The initial message sent immediately after subscribe causes the
    /// WebSocket client to disconnect. When the line
    ///   cfg.AddSingletonChannelSubscribers(typeof(TickersSubscriber))
    /// is commented out (no subscriber → no subscribe → no initial message),
    /// the problem goes away.
    ///
    /// EXPECTED: Client stays connected and receives the initial message.
    /// </summary>
    [Fact]
    public async Task WebSocket_ChannelWithInitialMessage_ShouldNotDisconnect()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();

        // Create channel with SendLastMessageAsInitial enabled
        await server.Rider.Channel.Create("Tickers", o =>
        {
            o.SendLastMessageAsInitial = true;
        });

        // Publish a message BEFORE any client connects — this becomes the initial message
        HorseChannel tickersChannel = server.Rider.Channel.Find("Tickers");
        Assert.NotNull(tickersChannel);
        tickersChannel.Push("{\"Symbol\":\"BTCUSD\",\"Price\":42000}");
        await Task.Delay(200); // let the channel process the message

        var (_, wsPort) = server.Start(requestTimeout: 5);
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            bool disconnected = false;
            int messagesReceived = 0;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-initial-msg")
                .SetClientType("Trading.Api")
                .AddSingletonChannelSubscribers(typeof(TestTickerSubscriber))
                .UseHorseOverWebSockets()
                .Build();

            client.DisconnectionOnAutoJoinFailure = true;
            client.Disconnected += _ => disconnected = true;
            client.MessageReceived += (_, _) => Interlocked.Increment(ref messagesReceived);

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            // Wait long enough for AutoSubscribe + initial message delivery + any timeout
            await Task.Delay(10_000);

            Assert.False(disconnected,
                "Client disconnected after subscribing to a channel with an initial message. " +
                "The initial message sent immediately after subscribe likely corrupts the WebSocket frame.");
            Assert.True(client.IsConnected, "Client should still be connected.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Same scenario as above but with MANUAL subscribe (matching the screenshot's
    /// OnConnected callback pattern: await Task.Delay(1000) then Subscribe).
    ///
    /// EXPECTED: Client subscribes, receives initial message, stays connected.
    /// </summary>
    [Fact]
    public async Task WebSocket_ManualSubscribeToChannelWithInitialMessage_ShouldNotDisconnect()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();

        // Create channel with initial message
        await server.Rider.Channel.Create("Tickers", o =>
        {
            o.SendLastMessageAsInitial = true;
        });

        HorseChannel tickersChannel = server.Rider.Channel.Find("Tickers");
        Assert.NotNull(tickersChannel);
        tickersChannel.Push("{\"Symbol\":\"BTCUSD\",\"Price\":42000}");
        await Task.Delay(200);

        var (_, wsPort) = server.Start(requestTimeout: 5);
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            bool disconnected = false;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-manual-initial")
                .SetClientType("Trading.Api")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            // Matches the screenshot: await Task.Delay(1000) then subscribe
            await Task.Delay(1000);
            Assert.True(client.IsConnected, "Client should be connected before subscribe.");

            HorseResult subResult = await client.Channel.Subscribe("Tickers", true, CancellationToken.None);

            // After subscribe, the initial message is sent immediately by the server
            await Task.Delay(3000);

            Assert.False(disconnected,
                "Client disconnected after receiving channel initial message over WebSocket.");
            Assert.True(client.IsConnected);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Control: Same initial-message scenario over normal Horse protocol — should work.
    /// </summary>
    [Fact]
    public async Task HorseProtocol_ChannelWithInitialMessage_StaysConnected()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();

        await server.Rider.Channel.Create("Tickers", o =>
        {
            o.SendLastMessageAsInitial = true;
        });

        HorseChannel tickersChannel = server.Rider.Channel.Find("Tickers");
        Assert.NotNull(tickersChannel);
        tickersChannel.Push("{\"Symbol\":\"BTCUSD\",\"Price\":42000}");
        await Task.Delay(200);

        var (horsePort, _) = server.Start(requestTimeout: 5);
        Assert.True(horsePort > 0, "Server failed to start");

        try
        {
            bool disconnected = false;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("horse-initial-msg")
                .SetClientType("Trading.Api")
                .AddSingletonChannelSubscribers(typeof(TestTickerSubscriber))
                .Build();

            client.DisconnectionOnAutoJoinFailure = true;
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"horse://localhost:{horsePort}");
            await Task.Delay(5000);

            Assert.False(disconnected, "Horse protocol client should not disconnect.");
            Assert.True(client.IsConnected);
        }
        finally
        {
            server.Stop();
        }
    }
}
