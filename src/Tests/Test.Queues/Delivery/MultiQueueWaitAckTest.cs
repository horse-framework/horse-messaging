using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class MultiQueueWaitAckTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SingleClient_MultipleQueues_WaitForAck_ConsumesBacklog(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        try
        {
            string[] queues = ["mq-ack-1", "mq-ack-2", "mq-ack-3", "mq-ack-4", "mq-ack-5"];

            foreach (string queue in queues)
                await ctx.Rider.Queue.Create(queue, o =>
                {
                    o.Type = QueueType.RoundRobin;
                    o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                    o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
                });

            HorseClient producer = new();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            foreach (string queue in queues)
            for (int i = 0; i < 10; i++)
                await producer.Queue.Push(queue, new MemoryStream(Encoding.UTF8.GetBytes($"msg-{queue}-{i}")), false, CancellationToken.None);

            ConcurrentDictionary<string, int> received = new();
            HorseClient consumer = new() { AutoAcknowledge = false };
            consumer.MessageReceived += (_, message) =>
            {
                received.AddOrUpdate(message.Target, 1, (_, count) => count + 1);
                Task ackTask = Task.Run(async () =>
                {
                    await Task.Delay(20);
                    await consumer.SendAsync(message.CreateAcknowledge(), CancellationToken.None);
                });
            };

            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("mq-ack-1", true, CancellationToken.None);
            await consumer.Queue.Subscribe("mq-ack-2", true, CancellationToken.None);
            await consumer.Queue.Subscribe("mq-ack-3", true, CancellationToken.None);

            for (int i = 0; i < 200; i++)
            {
                if (GetCount(received, "mq-ack-1") >= 10 &&
                    GetCount(received, "mq-ack-2") >= 10 &&
                    GetCount(received, "mq-ack-3") >= 10)
                    break;

                await Task.Delay(100);
            }

            Assert.Equal(10, GetCount(received, "mq-ack-1"));
            Assert.Equal(10, GetCount(received, "mq-ack-2"));
            Assert.Equal(10, GetCount(received, "mq-ack-3"));

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    private static int GetCount(ConcurrentDictionary<string, int> received, string queue)
    {
        return received.TryGetValue(queue, out int count) ? count : 0;
    }
}
