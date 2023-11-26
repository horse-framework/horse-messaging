using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Xunit;

namespace Test.Direct;

public class DirectMessageTest
{
    /// <summary>
    /// Sends a client message and does not wait any ack or response
    /// </summary>
    [Fact]
    public async Task WithoutAnyResponse()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start();

        HorseClient client1 = new HorseClient();
        HorseClient client2 = new HorseClient();

        client1.ClientId = "client-1";
        client2.ClientId = "client-2";

        await client1.ConnectAsync("horse://localhost:" + port);
        await client2.ConnectAsync("horse://localhost:" + port);

        Assert.True(client1.IsConnected);
        Assert.True(client2.IsConnected);

        bool received = false;
        client2.MessageReceived += (c, m) => received = m.Source == "client-1";

        HorseMessage message = new HorseMessage(MessageType.DirectMessage, "client-2");
        message.SetStringContent("Hello, World!");

        HorseResult sent = await client1.SendAsync(message);
        Assert.Equal(HorseResultCode.Ok, sent.Code);
        await Task.Delay(1000);
        Assert.True(received);
        server.Stop();
    }

    /// <summary>
    /// Sends a client message and waits acknowledge
    /// </summary>
    [Fact]
    public async Task WithAcknowledge()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start();

        HorseClient client1 = new HorseClient();
        HorseClient client2 = new HorseClient();

        client1.ClientId = "client-1";
        client2.ClientId = "client-2";
        client2.AutoAcknowledge = true;
        client1.ResponseTimeout = TimeSpan.FromSeconds(14);

        await client1.ConnectAsync("horse://localhost:" + port);
        await client2.ConnectAsync("horse://localhost:" + port);

        Assert.True(client1.IsConnected);
        Assert.True(client2.IsConnected);

        bool received = false;
        client2.MessageReceived += (c, m) => received = m.Source == "client-1";

        HorseMessage message = new HorseMessage(MessageType.DirectMessage, "client-2");
        message.SetStringContent("Hello, World!");

        HorseResult sent = await client1.SendAndGetAck(message);
        Assert.Equal(HorseResultCode.Ok, sent.Code);
        Assert.True(received);
        server.Stop();
    }

    /// <summary>
    /// Sends a client message and waits response
    /// </summary>
    [Fact]
    public async Task WithResponse()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start();

        HorseClient client1 = new HorseClient();
        HorseClient client2 = new HorseClient();

        client1.ClientId = "client-1";
        client2.ClientId = "client-2";
        client2.AutoAcknowledge = true;

        await client1.ConnectAsync("horse://localhost:" + port);
        await client2.ConnectAsync("horse://localhost:" + port);

        Assert.True(client1.IsConnected);
        Assert.True(client2.IsConnected);

        client2.MessageReceived += async (c, m) =>
        {
            if (m.Source == "client-1")
            {
                HorseMessage rmsg = m.CreateResponse(HorseResultCode.Ok);
                rmsg.SetStringContent("Hello, World Response!");
                await ((HorseClient) c).SendAsync(rmsg);
            }
        };

        HorseMessage message = new HorseMessage(MessageType.DirectMessage, "client-2");
        message.SetStringContent("Hello, World!");

        HorseMessage response = await client1.Request(message);
        Assert.NotNull(response);
        Assert.Equal(0, response.ContentType);
        server.Stop();
    }
}