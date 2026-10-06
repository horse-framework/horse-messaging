using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.OverWebSockets;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.WebSocket.Protocol;
using Xunit;

namespace Test.Websocket;

public class OverWebSocketTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disconnect_RequeuesRoundRobinDeliveryBeforeAckTimeout(bool serverClosesConnection)
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(horsePort > 0 && wsPort > 0);

        HorseClient consumer = CreateWebSocketClient();
        HorseClient replacement = CreateWebSocketClient();
        HorseClient producer = new HorseClient { AutoSubscribe = false };

        try
        {
            HorseQueue queue = await server.Rider.Queue.Create("ws-disconnect", options =>
            {
                options.Type = QueueType.RoundRobin;
                options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                options.AcknowledgeTimeout = TimeSpan.FromSeconds(60);
                options.PutBack = PutBackDecision.Regular;
            });

            TaskCompletionSource<HorseMessage> firstDelivery = ObserveQueueMessage(consumer);
            TaskCompletionSource<HorseMessage> redelivery = ObserveQueueMessage(replacement);
            replacement.AutoAcknowledge = true;

            await consumer.ConnectAsync($"ws://localhost:{wsPort}");
            Assert.Equal(HorseResultCode.Ok, (await consumer.Queue.Subscribe(queue.Name, true, CancellationToken.None)).Code);
            var horseSocket = server.Rider.Client.Find(consumer.ClientId);
            Assert.NotNull(horseSocket);
            int disconnectEvents = 0;
            horseSocket.Disconnected += _ => Interlocked.Increment(ref disconnectEvents);

            await producer.ConnectAsync($"horse://localhost:{horsePort}");
            Assert.Equal(HorseResultCode.Ok,
                (await producer.Queue.Push(queue.Name, new MemoryStream("pending-ack"u8.ToArray()), true, CancellationToken.None)).Code);
            HorseMessage original = await firstDelivery.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(original.WaitResponse);

            await replacement.ConnectAsync($"ws://localhost:{wsPort}");
            Assert.Equal(HorseResultCode.Ok, (await replacement.Queue.Subscribe(queue.Name, true, CancellationToken.None)).Code);

            if (serverClosesConnection)
                horseSocket.Info.Socket.Disconnect();
            else
                consumer.Disconnect();

            await WaitUntil(() => server.Rider.Client.Find(consumer.ClientId) == null);
            Assert.False(horseSocket.IsConnected);
            Assert.Equal(1, Volatile.Read(ref disconnectEvents));
            Assert.Null(queue.FindClient(horseSocket));

            HorseMessage retried = await redelivery.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(original.MessageId, retried.MessageId);
            Assert.Equal("pending-ack", retried.ToString());
        }
        finally
        {
            consumer.Disconnect();
            replacement.Disconnect();
            producer.Disconnect();
            server.Stop();
        }
    }

    [Fact]
    public async Task PushQueue_DeliversToWebSocketAndHorseConsumers()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(horsePort > 0 && wsPort > 0);

        HorseClient wsConsumer = CreateWebSocketClient();
        HorseClient horseConsumer = new HorseClient { AutoSubscribe = false };
        HorseClient producer = new HorseClient { AutoSubscribe = false };

        try
        {
            HorseQueue queue = await server.Rider.Queue.Create("ws-push", options =>
            {
                options.Type = QueueType.Push;
                options.Acknowledge = QueueAckDecision.None;
            });
            TaskCompletionSource<HorseMessage> wsDelivery = ObserveQueueMessage(wsConsumer);
            TaskCompletionSource<HorseMessage> horseDelivery = ObserveQueueMessage(horseConsumer);

            await wsConsumer.ConnectAsync($"ws://localhost:{wsPort}");
            await horseConsumer.ConnectAsync($"horse://localhost:{horsePort}");
            Assert.Equal(HorseResultCode.Ok, (await wsConsumer.Queue.Subscribe(queue.Name, true, CancellationToken.None)).Code);
            Assert.Equal(HorseResultCode.Ok, (await horseConsumer.Queue.Subscribe(queue.Name, true, CancellationToken.None)).Code);

            await producer.ConnectAsync($"horse://localhost:{horsePort}");
            string content = new string('x', 300);
            Assert.Equal(HorseResultCode.Ok,
                (await producer.Queue.Push(queue.Name, new MemoryStream(Encoding.UTF8.GetBytes(content)), true, CancellationToken.None)).Code);

            HorseMessage wsMessage = await wsDelivery.Task.WaitAsync(TimeSpan.FromSeconds(5));
            HorseMessage horseMessage = await horseDelivery.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(content, wsMessage.ToString());
            Assert.Equal(content, horseMessage.ToString());
            Assert.Equal(horseMessage.MessageId, wsMessage.MessageId);
            Assert.True(wsConsumer.IsConnected);
        }
        finally
        {
            wsConsumer.Disconnect();
            horseConsumer.Disconnect();
            producer.Disconnect();
            server.Stop();
        }
    }

    [Fact]
    public async Task ServerReadOnlyMemorySend_IsWrappedInWebSocketFrame()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (_, wsPort) = server.Start();
        Assert.True(wsPort > 0);
        HorseClient client = CreateWebSocketClient();

        try
        {
            TaskCompletionSource<HorseMessage> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.MessageReceived += (_, message) =>
            {
                if (message.Type == MessageType.DirectMessage)
                    received.TrySetResult(message);
            };
            await client.ConnectAsync($"ws://localhost:{wsPort}");
            await WaitUntil(() => server.Rider.Client.Find(client.ClientId)?.SwitchingProtocol != null);

            HorseMessage outgoing = new(MessageType.DirectMessage)
            {
                Content = new MemoryStream("memory-send"u8.ToArray())
            };
            outgoing.SetMessageId("memory-send-id");
            var horseSocket = server.Rider.Client.Find(client.ClientId);
            Assert.True(await horseSocket.SendRawAsync(new ReadOnlyMemory<byte>(HorseProtocolWriter.Create(outgoing))));

            HorseMessage incoming = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(outgoing.MessageId, incoming.MessageId);
            Assert.Equal("memory-send", incoming.ToString());
        }
        finally
        {
            client.Disconnect();
            server.Stop();
        }
    }

    [Theory]
    [InlineData(SocketOpCode.Ping)]
    [InlineData(SocketOpCode.Pong)]
    public async Task ClientControlFrames_AreMaskedEvenWithEmptyPayload(SocketOpCode opCode)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        HorseClient client = CreateWebSocketClient();
        client.PingInterval = TimeSpan.Zero;

        try
        {
            Task connecting = Task.Run(() => client.ConnectAsync($"ws://localhost:{port}"));
            using TcpClient peer = await listener.AcceptTcpClientAsync(timeout.Token);
            NetworkStream stream = peer.GetStream();
            string key = null;
            using (StreamReader request = new(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
            {
                string line;
                while (!string.IsNullOrEmpty(line = await request.ReadLineAsync(timeout.Token)))
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                        key = line[(line.IndexOf(':') + 1)..].Trim();
            }

            Assert.NotNull(key);
            string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            string response = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
            await connecting.WaitAsync(timeout.Token);
            Assert.NotNull(await new WebSocketReader(null).Read(stream).WaitAsync(timeout.Token));

            if (opCode == SocketOpCode.Ping)
                client.SwitchingProtocol.Ping();
            else
                client.SwitchingProtocol.Pong();

            byte[] header = new byte[2];
            await stream.ReadExactlyAsync(header, timeout.Token);
            Assert.Equal((byte)(0x80 | (byte)opCode), header[0]);
            Assert.Equal(0x80, header[1]);
            byte[] mask = new byte[4];
            await stream.ReadExactlyAsync(mask, timeout.Token);
        }
        finally
        {
            client.Disconnect();
        }
    }

    private static HorseClient CreateWebSocketClient()
    {
        HorseClient client = new() { AutoSubscribe = false, AutoAcknowledge = false };
        client.SetClientId(Guid.NewGuid().ToString("N"));
        client.UseHorseOverWebSockets();
        return client;
    }

    private static TaskCompletionSource<HorseMessage> ObserveQueueMessage(HorseClient client)
    {
        TaskCompletionSource<HorseMessage> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += (_, message) =>
        {
            if (message.Type == MessageType.QueueMessage)
                received.TrySetResult(message);
        };
        return received;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
        Assert.True(condition(), "Condition was not met within five seconds.");
    }
}
