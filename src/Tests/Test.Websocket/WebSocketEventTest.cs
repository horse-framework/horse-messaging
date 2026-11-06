using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.OverWebSockets;
using Xunit;

namespace Test.Websocket;

public class WebSocketEventTest
{
    [Fact]
    public async Task EventSubscriberOverWebSocket_ReceivesFramedEventAndConnectionStaysUsable()
    {
        WebSocketTestServer server = new WebSocketTestServer();
        await server.Initialize();
        var (horsePort, wsPort) = server.Start();
        Assert.True(horsePort > 0 && wsPort > 0);

        HorseClient subscriber = new() { AutoSubscribe = false, CatchEventMessages = true };
        subscriber.SetClientId(Guid.NewGuid().ToString("N"));
        subscriber.UseHorseOverWebSockets();

        HorseClient other = new HorseClient { AutoSubscribe = false };

        TaskCompletionSource<HorseMessage> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        subscriber.MessageReceived += (_, message) =>
        {
            if (message.Type == MessageType.Event && message.ContentType == (ushort) HorseEventType.QueueCreate)
                received.TrySetResult(message);
        };

        try
        {
            await subscriber.ConnectAsync($"ws://localhost:{wsPort}");
            HorseResult subscribeResult = await subscriber.Event.Subscribe(HorseEventType.QueueCreate, null, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await other.ConnectAsync($"horse://localhost:{horsePort}");
            Assert.Equal(HorseResultCode.Ok, (await other.Queue.Create("ws-event-queue", CancellationToken.None)).Code);

            HorseMessage eventMessage = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("ws-event-queue", eventMessage.Target);

            // Raw, unframed bytes would corrupt the WebSocket stream; a later request must still work.
            HorseResult afterEvent = await subscriber.Queue.Create("ws-after-event", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, afterEvent.Code);
            Assert.True(subscriber.IsConnected);
        }
        finally
        {
            subscriber.Disconnect();
            other.Disconnect();
            server.Stop();
        }
    }
}
