using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Test.Common;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues;

public class PutBackTest
{
    [Fact]
    public async Task Delayed()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseQueue queue = server.Rider.Queue.Find("push-a");
        queue.Options.PutBackDelay = 2000;
        queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        server.PutBack = PutBackDecision.Regular;
        queue.Options.PutBack = PutBackDecision.Regular;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        Assert.True(producer.IsConnected);

        await producer.Queue.Push("push-a", "First", false);
        await Task.Delay(100);
        await producer.Queue.Push("push-a", "Second", false);
        await Task.Delay(200);
        Assert.Equal(2, queue.Manager.MessageStore.Count() + queue.Manager.DeliveryHandler.Tracker.GetDeliveryCount());

        int receivedMessages = 0;
        HorseClient consumer = new HorseClient();
        consumer.ClientId = "consumer";
        await consumer.ConnectAsync("horse://localhost:" + port);
        Assert.True(consumer.IsConnected);
        consumer.MessageReceived += async (c, m) =>
        {
            receivedMessages++;
            await consumer.Queue.Unsubscribe("push-a", true);
            await Task.Delay(1000);
            await consumer.SendNegativeAck(m);
        };

        HorseResult joined = await consumer.Queue.Subscribe("push-a", true);
        Assert.Equal(HorseResultCode.Ok, joined.Code);

        await Task.Delay(1500);
        Assert.Equal(1, receivedMessages);
        Assert.Equal(1, queue.Manager.MessageStore.Count());
            
        await Task.Delay(3000);
            
        int totalMessages = queue.Manager.MessageStore.Count();
        if (queue.ProcessingMessage != null)
            totalMessages++;
            
        Assert.Equal(2, totalMessages);
        server.Stop();
    }

    [Fact]
    public async Task NoDelay()
    {
        TestHorseRider server = new TestHorseRider();
        await server.Initialize();
        int port = server.Start(300, 300);

        HorseQueue queue = server.Rider.Queue.Find("push-a");
        queue.Options.PutBackDelay = 0;
        queue.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        server.PutBack = PutBackDecision.Regular;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        Assert.True(producer.IsConnected);

        await producer.Queue.Push("push-a", "First", false);
        await Task.Delay(100);
        await producer.Queue.Push("push-a", "Second", false);
        await Task.Delay(200);
        Assert.Equal(2, queue.Manager.MessageStore.Count());

        int receivedMessages = 0;
        HorseClient consumer = new HorseClient();
        consumer.ClientId = "consumer";
        await consumer.ConnectAsync("horse://localhost:" + port);
        Assert.True(consumer.IsConnected);
        consumer.MessageReceived += async (c, m) =>
        {
            receivedMessages++;
            await consumer.Queue.Unsubscribe("push-a", true);
            await Task.Delay(1000);
            await consumer.SendNegativeAck(m);
        };

        HorseResult joined = await consumer.Queue.Subscribe("push-a", true);
        Assert.Equal(HorseResultCode.Ok, joined.Code);

        await Task.Delay(1500);
        Assert.Equal(1, receivedMessages);
        Assert.Equal(2, queue.Manager.MessageStore.Count());
        server.Stop();
    }
}