using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Network;

/// <summary>
/// Integration tests for client-server connection lifecycle:
/// connect, disconnect, reconnect, ping/pong, multiple clients.
/// </summary>
public class ConnectionTest
{
    #region Connect / Disconnect

    [Fact]
    public async Task ClientConnectsToServer()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task ClientDisconnectsCleanly()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            client.Disconnect();
            await Task.Delay(200);

            Assert.False(client.IsConnected);
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task MultipleClientsConnect()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client1 = new HorseClient();
            HorseClient client2 = new HorseClient();
            HorseClient client3 = new HorseClient();

            await client1.ConnectAsync($"horse://localhost:{port}");
            await client2.ConnectAsync($"horse://localhost:{port}");
            await client3.ConnectAsync($"horse://localhost:{port}");

            await Task.Delay(500);

            Assert.True(client1.IsConnected);
            Assert.True(client2.IsConnected);
            Assert.True(client3.IsConnected);

            client1.Disconnect();
            client2.Disconnect();
            client3.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task ConnectToInvalidPort_Fails()
    {
        HorseClient client = new HorseClient();

        await client.ConnectAsync("horse://localhost:59999");
        await Task.Delay(500);

        Assert.False(client.IsConnected);
    }

    #endregion

    #region Client Identification

    [Fact]
    public async Task ClientReceivesId()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);
            Assert.False(string.IsNullOrEmpty(client.ClientId));

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task ClientNameAndType()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            client.SetClientName("test-client");
            client.SetClientType("test-type");
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task DuplicateClientId_SecondRejected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            string sharedId = "dup-" + Guid.NewGuid().ToString("N")[..8];

            HorseClient client1 = new HorseClient();
            client1.SetClientId(sharedId);
            await client1.ConnectAsync($"horse://localhost:{port}");

            for (int i = 0; i < 20 && !client1.IsConnected; i++)
                await Task.Delay(100);
            Assert.True(client1.IsConnected);

            bool client2Disconnected = false;
            HorseClient client2 = new HorseClient();
            client2.SetClientId(sharedId);
            client2.Disconnected += _ => client2Disconnected = true;
            await client2.ConnectAsync($"horse://localhost:{port}");

            for (int i = 0; i < 50 && !client2Disconnected; i++)
                await Task.Delay(100);

            client2.Disconnect();

            Assert.True(client2Disconnected);

            client1.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Message Send/Receive

    [Fact]
    public async Task SendDirectMessage()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync($"horse://localhost:{port}");

            HorseClient receiver = new HorseClient();

            HorseMessage receivedMessage = null;
            receiver.MessageReceived += (_, m) => { receivedMessage = m; };

            await receiver.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(1000);

            Assert.True(sender.IsConnected);
            Assert.True(receiver.IsConnected);

            HorseMessage msg = new HorseMessage(MessageType.DirectMessage, receiver.ClientId);
            msg.SetStringContent("hello from sender");

            HorseResult result = await sender.SendAsync(msg, CancellationToken.None);
            Assert.NotNull(result);

            await Task.Delay(2000);

            Assert.NotNull(receivedMessage);
            Assert.Equal("hello from sender", receivedMessage.ToString());

            sender.Disconnect();
            receiver.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task SendMessageToNonExistentClient()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "non-existent-client");
            msg.SetStringContent("hello");
            msg.WaitResponse = true;

            HorseResult result = await client.SendAsync(msg, CancellationToken.None);
            Assert.NotNull(result);

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Ping/Pong KeepAlive

    [Fact]
    public async Task ConnectionStaysAliveWithPing()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            await Task.Delay(5000);

            Assert.True(client.IsConnected);

            client.Disconnect();
        }, pingInterval: 2, requestTimeout: 10);
    }

    #endregion

    #region Server Stop

    [Fact]
    public async Task ServerStop_DisconnectsClients()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            bool disconnected = false;
            HorseClient client = new HorseClient();
            client.Disconnected += _ => disconnected = true;
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);

            await server.StopAsync();

            for (int i = 0; i < 150 && !disconnected; i++)
                await Task.Delay(100);

            client.Disconnect();

            Assert.True(disconnected);
        }, pingInterval: 2, requestTimeout: 4);
    }

    #endregion
}
