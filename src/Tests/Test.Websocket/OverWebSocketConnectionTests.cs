using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.OverWebSockets;
using Xunit;

namespace Test.Websocket;

public class OverWebSocketConnectionTests
{
    /// <summary>
    /// Tests that a client can connect to the server over WebSocket
    /// and remains connected after the initial handshake.
    /// </summary>
    [Fact]
    public async Task Client_ShouldStayConnected_AfterWebSocketHandshake()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-test-client")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            await Task.Delay(2000);

            Assert.True(client.IsConnected, "Client should remain connected after WebSocket handshake, but it was disconnected.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that a client connected over WebSocket receives the Accepted message
    /// and the ClientId is properly set by the server.
    /// </summary>
    [Fact]
    public async Task Client_ShouldReceiveAcceptedMessage_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            bool connected = false;
            bool disconnected = false;
            string receivedClientId = null;

            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-accepted-test")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;
            client.Connected += c =>
            {
                connected = true;
                receivedClientId = c.ClientId;
            };
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            await Task.Delay(3000);

            Assert.True(connected, "Connected event should have been triggered.");
            Assert.False(disconnected, "Client should NOT have been disconnected, but Disconnected event was triggered.");
            Assert.False(string.IsNullOrEmpty(receivedClientId), "Client should have received a client ID from the server.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that a client connected over WebSocket stays alive
    /// after multiple ping intervals.
    /// </summary>
    [Fact]
    public async Task Client_ShouldSurvivePingCycles_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start(pingInterval: 3, requestTimeout: 10);
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-ping-test")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;
            client.PingInterval = TimeSpan.FromSeconds(3);

            await client.ConnectAsync($"ws://localhost:{wsPort}");

            await Task.Delay(10000);

            Assert.True(client.IsConnected, "Client should remain connected after multiple ping cycles over WebSocket.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that multiple clients can connect over WebSocket simultaneously
    /// and all remain connected.
    /// </summary>
    [Fact]
    public async Task MultipleClients_ShouldStayConnected_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            const int clientCount = 3;
            HorseClient[] clients = new HorseClient[clientCount];

            for (int i = 0; i < clientCount; i++)
            {
                clients[i] = new HorseClientBuilder()
                    .SetClientName($"ws-multi-{i}")
                    .SetClientType("test-type")
                    .UseHorseOverWebSockets()
                    .Build();

                clients[i].AutoSubscribe = false;
                await clients[i].ConnectAsync($"ws://localhost:{wsPort}");
            }

            await Task.Delay(2000);

            for (int i = 0; i < clientCount; i++)
            {
                Assert.True(clients[i].IsConnected, $"Client {i} should remain connected over WebSocket, but it was disconnected.");
            }
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Compares WebSocket connectivity with direct Horse protocol connectivity.
    /// </summary>
    [Fact]
    public async Task WebSocketClient_ShouldBehaveIdentically_ToHorseProtocolClient()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(wsPort > 0 && horsePort > 0, "Server failed to start");

        try
        {
            HorseClient horseClient = new HorseClientBuilder()
                .SetClientName("horse-direct-client")
                .SetClientType("test-type")
                .Build();
            horseClient.AutoSubscribe = false;
            await horseClient.ConnectAsync($"horse://localhost:{horsePort}");

            HorseClient wsClient = new HorseClientBuilder()
                .SetClientName("ws-compare-client")
                .SetClientType("test-type")
                .UseHorseOverWebSockets()
                .Build();
            wsClient.AutoSubscribe = false;
            await wsClient.ConnectAsync($"ws://localhost:{wsPort}");

            await Task.Delay(2000);

            Assert.True(horseClient.IsConnected, "Horse protocol client should be connected.");
            Assert.True(wsClient.IsConnected, "WebSocket client should remain connected, just like the Horse protocol client.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that a client connected over WebSocket can subscribe to a channel
    /// and the subscription is confirmed by the server.
    /// </summary>
    [Fact]
    public async Task Client_ShouldSubscribeToChannel_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-channel-client")
                .SetClientType("channel-subscriber")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(1000);

            Assert.True(client.IsConnected, "Client should be connected before subscribing to channel.");

            HorseResult subscribeResult = await client.Channel.Subscribe("test-channel", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);
            Assert.True(client.IsConnected, "Client should remain connected after subscribing to a channel over WebSocket.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that a WebSocket client with name and type is visible in the server's connected client list.
    /// After connecting, fetches the client list from the server and verifies at least 1 client is present.
    /// </summary>
    [Fact]
    public async Task ConnectedClient_ShouldAppearInClientList_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient wsClient = new HorseClientBuilder()
                .SetClientName("ws-listed-client")
                .SetClientType("ws-test-type")
                .UseHorseOverWebSockets()
                .Build();

            wsClient.AutoSubscribe = false;

            await wsClient.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(2000);

            Assert.True(wsClient.IsConnected, "WebSocket client should be connected.");

            // Use a second client over Horse protocol to query the client list
            HorseClient queryClient = new HorseClientBuilder()
                .SetClientName("query-client")
                .SetClientType("query-type")
                .Build();

            queryClient.AutoSubscribe = false;
            await queryClient.ConnectAsync($"horse://localhost:{horsePort}");
            await Task.Delay(1000);

            Assert.True(queryClient.IsConnected, "Query client should be connected.");

            var result = await queryClient.Connection.GetConnectedClients(CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Result.Code);
            Assert.NotNull(result.Model);
            Assert.True(result.Model.Count >= 2, $"Server should have at least 2 connected clients (ws + query), but found {result.Model.Count}.");

            ClientInformation wsInfo = result.Model.FirstOrDefault(c => c.Name == "ws-listed-client");
            Assert.NotNull(wsInfo);
            Assert.Equal("ws-test-type", wsInfo.Type);
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests that a WebSocket client can subscribe to a channel and then be verified
    /// as a subscriber on the server side via the connected client list.
    /// Sets client name, type, subscribes to a channel, and verifies the client appears in the server's list.
    /// </summary>
    [Fact]
    public async Task Client_ShouldSubscribeChannelAndBeVisibleInClientList_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient wsClient = new HorseClientBuilder()
                .SetClientName("ws-subscriber")
                .SetClientType("subscriber-type")
                .UseHorseOverWebSockets()
                .Build();

            wsClient.AutoSubscribe = false;

            await wsClient.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(1000);

            Assert.True(wsClient.IsConnected, "WebSocket client should be connected before channel subscribe.");

            // Subscribe to a channel
            HorseResult subscribeResult = await wsClient.Channel.Subscribe("events-channel", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(500);
            Assert.True(wsClient.IsConnected, "WebSocket client should remain connected after channel subscription.");

            // Verify via server-side rider that client is in the list
            int clientCount = server.Rider.Client.GetOnlineClients();
            Assert.True(clientCount >= 1, $"Server should have at least 1 connected client, but found {clientCount}.");

            var connectedClient = server.Rider.Client.Clients.FirstOrDefault(c => c.Name == "ws-subscriber");
            Assert.NotNull(connectedClient);
            Assert.Equal("subscriber-type", connectedClient.Type);

            // Verify channel has subscribers
            var channel = server.Rider.Channel.Find("events-channel");
            Assert.NotNull(channel);
            Assert.True(channel.ClientsCount() >= 1, "Channel should have at least 1 subscriber.");
        }
        finally
        {
            server.Stop();
        }
    }

    /// <summary>
    /// Tests the full scenario: connect over WebSocket with client name and type,
    /// auto-subscribe to a channel, fetch client list via Connection.GetConnectedClients,
    /// and verify at least 1 client is connected with correct name and type.
    /// </summary>
    [Fact]
    public async Task FullScenario_ConnectSubscribeFetchClients_OverWebSocket()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0, "Server failed to start");

        try
        {
            HorseClient client = new HorseClientBuilder()
                .SetClientName("ws-full-test")
                .SetClientType("full-test-type")
                .UseHorseOverWebSockets()
                .Build();

            client.AutoSubscribe = false;

            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await Task.Delay(1500);

            Assert.True(client.IsConnected, "Client should be connected over WebSocket.");

            // Subscribe to channel
            HorseResult subResult = await client.Channel.Subscribe("my-channel", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subResult.Code);

            // Fetch client list from the same WebSocket connection
            var clientListResult = await client.Connection.GetConnectedClients(CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, clientListResult.Result.Code);
            Assert.NotNull(clientListResult.Model);
            Assert.True(clientListResult.Model.Count >= 1, $"Should have at least 1 connected client, but found {clientListResult.Model.Count}.");

            ClientInformation self = clientListResult.Model.FirstOrDefault(c => c.Name == "ws-full-test");
            Assert.NotNull(self);
            Assert.Equal("full-test-type", self.Type);
        }
        finally
        {
            server.Stop();
        }
    }
}
