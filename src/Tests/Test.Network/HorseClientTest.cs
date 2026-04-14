using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for HorseClient configuration API: properties, identity, events, host management.
/// </summary>
public class HorseClientTest
{
    #region Client Identity & Properties

    [Fact]
    public void SetClientId_BeforeConnect()
    {
        HorseClient client = new HorseClient();
        client.SetClientId("my-custom-id");
        Assert.Equal("my-custom-id", client.ClientId);
    }

    [Fact]
    public void SetClientId_ViaProperty()
    {
        HorseClient client = new HorseClient();
        client.ClientId = "prop-id";
        Assert.Equal("prop-id", client.ClientId);
    }

    [Fact]
    public void ClientId_ThrowsIfAlreadySet_ViaProperty()
    {
        HorseClient client = new HorseClient();
        client.ClientId = "first";

        Assert.Throws<InvalidOperationException>(() => client.ClientId = "second");
    }

    [Fact]
    public void SetClientName_SetsProperty()
    {
        HorseClient client = new HorseClient();
        client.SetClientName("test-name");
        client.SetClientName("updated-name");
    }

    [Fact]
    public void SetClientType_SetsProperty()
    {
        HorseClient client = new HorseClient();
        client.SetClientType("worker");
        client.SetClientType("consumer");
    }

    [Fact]
    public void SetClientToken_SetsProperty()
    {
        HorseClient client = new HorseClient();
        client.SetClientToken("bearer-xyz");
        client.SetClientToken("bearer-abc");
    }

    [Fact]
    public void AddProperty_And_RemoveProperty()
    {
        HorseClient client = new HorseClient();
        client.AddProperty("custom-key", "custom-value");
        client.RemoveProperty("custom-key");
    }

    #endregion

    #region Host Management

    [Fact]
    public void AddHost_DoesNotThrow()
    {
        HorseClient client = new HorseClient();
        client.AddHost("horse://server1:2626");
        client.AddHost("horse://server2:2626");
    }

    [Fact]
    public void AddHost_DuplicatesIgnored()
    {
        HorseClient client = new HorseClient();
        client.AddHost("horse://server1:2626");
        client.AddHost("horse://server1:2626");
    }

    [Fact]
    public void AddHost_ThrowsOnNull()
    {
        HorseClient client = new HorseClient();
        Assert.Throws<Exception>(() => client.AddHost(null));
    }

    [Fact]
    public void AddHost_ThrowsOnEmpty()
    {
        HorseClient client = new HorseClient();
        Assert.Throws<Exception>(() => client.AddHost(string.Empty));
    }

    #endregion

    #region Default Values

    [Fact]
    public void DefaultValues()
    {
        HorseClient client = new HorseClient();

        Assert.False(client.IsConnected);
        Assert.True(client.AutoSubscribe);
        Assert.True(client.DisconnectionOnAutoJoinFailure);
        Assert.False(client.AutoAcknowledge);
        Assert.False(client.CatchResponseMessages);
        Assert.False(client.CatchEventMessages);
        Assert.False(client.ThrowExceptions);
        Assert.True(client.SmartHealthCheck);
        Assert.True(client.AutoDiscardSwitchingProtocol);
        Assert.Equal(TimeSpan.FromSeconds(30), client.ResponseTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), client.PullTimeout);
        Assert.Equal(TimeSpan.FromSeconds(15), client.PingInterval);
        Assert.Equal(TimeSpan.FromSeconds(3), client.ReconnectWait);
        Assert.NotNull(client.UniqueIdGenerator);
        Assert.NotNull(client.MessageSerializer);
        Assert.NotNull(client.Cache);
        Assert.NotNull(client.Direct);
        Assert.NotNull(client.Channel);
        Assert.NotNull(client.Queue);
        Assert.NotNull(client.Connection);
        Assert.NotNull(client.Router);
        Assert.NotNull(client.Event);
    }

    #endregion

    #region Connect Event

    [Fact]
    public async Task ConnectedEvent_Fires()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            bool connected = false;
            HorseClient client = new HorseClient();
            client.Connected += _ => connected = true;

            await client.ConnectAsync($"horse://localhost:{port}");

            for (int i = 0; i < 20 && !connected; i++)
                await Task.Delay(100);

            Assert.True(connected);
            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task DisconnectedEvent_Fires()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            bool disconnected = false;
            HorseClient client = new HorseClient();
            client.Disconnected += _ => disconnected = true;

            await client.ConnectAsync($"horse://localhost:{port}");

            for (int i = 0; i < 20 && !client.IsConnected; i++)
                await Task.Delay(100);

            client.Disconnect();

            for (int i = 0; i < 20 && !disconnected; i++)
                await Task.Delay(100);

            Assert.True(disconnected);
        }, pingInterval: 3, requestTimeout: 15);
    }

    [Fact]
    public async Task ErrorEvent_DoesNotThrowByDefault()
    {
        HorseClient client = new HorseClient();
        bool errorFired = false;
        client.Error += (_, _, _) => errorFired = true;

        await client.ConnectAsync("horse://localhost:59998");
        await Task.Delay(500);

        Assert.False(client.IsConnected);
        _ = errorFired;
    }

    #endregion

    #region Send Without Connection

    [Fact]
    public async Task SendAsync_WithoutConnection_ReturnsError()
    {
        HorseClient client = new HorseClient();
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetStringContent("test");

        HorseResult result = await client.SendAsync(msg, CancellationToken.None);

        Assert.Equal(HorseResultCode.SendError, result.Code);
    }

    [Fact]
    public void Send_WithoutConnection_ReturnsFalse()
    {
        HorseClient client = new HorseClient();
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetStringContent("test");

        bool sent = client.Send(msg);

        Assert.False(sent);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        HorseClient client = new HorseClient();
        client.Dispose();
    }

    [Fact]
    public async Task Dispose_AfterConnect()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);
            Assert.True(client.IsConnected);

            client.Disconnect();
            client.Dispose();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Acknowledge Flow

    [Fact]
    public async Task SendDirectMessage_WithAck()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            HorseClient sender = new HorseClient();
            await sender.ConnectAsync($"horse://localhost:{port}");

            HorseClient receiver = new HorseClient();
            receiver.AutoAcknowledge = true;
            await receiver.ConnectAsync($"horse://localhost:{port}");

            for (int i = 0; i < 20 && (!sender.IsConnected || !receiver.IsConnected); i++)
                await Task.Delay(100);

            HorseMessage msg = new HorseMessage(MessageType.DirectMessage, receiver.ClientId);
            msg.SetStringContent("ack test");

            HorseResult result = await sender.SendAsync(msg, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            sender.Disconnect();
            receiver.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion

    #region Client Id Assignment

    [Fact]
    public async Task ServerAssignsClientId()
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
    public async Task CustomClientId_Preserved()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Assert.True(port > 0);

            string customId = "custom-" + Guid.NewGuid().ToString("N")[..8];
            HorseClient client = new HorseClient();
            client.SetClientId(customId);
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(500);

            Assert.True(client.IsConnected);
            Assert.False(string.IsNullOrEmpty(client.ClientId));

            client.Disconnect();
        }, pingInterval: 3, requestTimeout: 15);
    }

    #endregion
}
