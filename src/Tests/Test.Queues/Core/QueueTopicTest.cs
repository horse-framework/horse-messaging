using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Core;

public class QueueTopicTest
{
    #region Topic Assignment

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SetTopic_ServerSide_TopicIsStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("topic-q1", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("topic-q1");
            Assert.NotNull(queue);

            queue.Topic = "orders";
            Assert.Equal("orders", queue.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SetTopic_ViaHeader_OnAutoCreate(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            var producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            var headers = new List<KeyValuePair<string, string>>
                    {
                        new(HorseHeaders.QUEUE_TOPIC, "payments")
                    };

            // Push to a non-existent queue with Queue-Topic header → auto-create with topic
            await producer.Queue.Push("topic-auto-q", new MemoryStream("test"u8.ToArray()), true, headers, CancellationToken.None);

            HorseQueue queue = ctx.Rider.Queue.Find("topic-auto-q");
            Assert.NotNull(queue);
            Assert.Equal("payments", queue.Topic);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SetTopic_NullByDefault(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("no-topic-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("no-topic-q");

            Assert.NotNull(queue);
            Assert.Null(queue.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UpdateTopic_ChangesValue(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("update-topic-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("update-topic-q");
            Assert.NotNull(queue);

            queue.Topic = "first-topic";
            Assert.Equal("first-topic", queue.Topic);

            queue.Topic = "second-topic";
            Assert.Equal("second-topic", queue.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClearTopic_SetToNull(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("clear-topic-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("clear-topic-q");
            Assert.NotNull(queue);

            queue.Topic = "temp-topic";
            Assert.Equal("temp-topic", queue.Topic);

            queue.Topic = null;
            Assert.Null(queue.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Topic in QueueInformation (List API)

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ListQueues_TopicIncludedInResponse(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("listed-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("listed-q");
            queue.Topic = "inventory";

            var client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{ctx.Port}");

            var result = await client.Queue.List(CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Result.Code);

            QueueInformation info = result.Model.FirstOrDefault(q => q.Name == "listed-q");
            Assert.NotNull(info);
            Assert.Equal("inventory", info.Topic);

            client.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ListQueues_NullTopic_ReturnsNullInInfo(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("no-topic-listed-q", o => o.Type = QueueType.Push);

            var client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{ctx.Port}");

            var result = await client.Queue.List(CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Result.Code);

            QueueInformation info = result.Model.FirstOrDefault(q => q.Name == "no-topic-listed-q");
            Assert.NotNull(info);
            Assert.Null(info.Topic);

            client.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Topic in QueueConfiguration (Persistence)

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task QueueConfiguration_TopicPersisted(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("cfg-topic-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("cfg-topic-q");
            queue.Topic = "config-test";

            // Force configuration save
            var configurator = ctx.Rider.Queue.OptionsConfigurator;
            if (configurator != null)
            {
                var cfg = configurator.Find(x => x.Name == "cfg-topic-q");
                if (cfg != null)
                {
                    cfg.Topic = queue.Topic;
                    configurator.Save();
                }
            }

            // Verify the configuration object matches
            var config = QueueConfiguration.Create(queue);
            Assert.Equal("config-test", config.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Multiple Queues with Different Topics

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MultipleQueues_DifferentTopics_IndependentValues(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("q-orders", o => o.Type = QueueType.Push);
            await ctx.Rider.Queue.Create("q-payments", o => o.Type = QueueType.Push);
            await ctx.Rider.Queue.Create("q-notifications", o => o.Type = QueueType.Push);

            ctx.Rider.Queue.Find("q-orders").Topic = "commerce";
            ctx.Rider.Queue.Find("q-payments").Topic = "commerce";
            ctx.Rider.Queue.Find("q-notifications").Topic = "alerts";

            Assert.Equal("commerce", ctx.Rider.Queue.Find("q-orders").Topic);
            Assert.Equal("commerce", ctx.Rider.Queue.Find("q-payments").Topic);
            Assert.Equal("alerts", ctx.Rider.Queue.Find("q-notifications").Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FindQueues_ByTopic_FilterWorks(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("ft-q1", o => o.Type = QueueType.Push);
            await ctx.Rider.Queue.Create("ft-q2", o => o.Type = QueueType.Push);
            await ctx.Rider.Queue.Create("ft-q3", o => o.Type = QueueType.Push);

            ctx.Rider.Queue.Find("ft-q1").Topic = "billing";
            ctx.Rider.Queue.Find("ft-q2").Topic = "billing";
            ctx.Rider.Queue.Find("ft-q3").Topic = "shipping";

            var billingQueues = ctx.Rider.Queue.Queues
                .Where(q => q.Topic == "billing")
                .ToList();

            Assert.Equal(2, billingQueues.Count);
            Assert.All(billingQueues, q => Assert.Equal("billing", q.Topic));

            var shippingQueues = ctx.Rider.Queue.Queues
                .Where(q => q.Topic == "shipping")
                .ToList();

            Assert.Single(shippingQueues);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Topic via Header on Push (Auto-Create Queue with Topic)

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithTopicHeader_AutoCreatesQueueWithTopic(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            var producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            var headers = new List<KeyValuePair<string, string>>
                    {
                        new(HorseHeaders.QUEUE_TOPIC, "financial")
                    };

            await producer.Queue.Push("auto-topic-push", new MemoryStream("data"u8.ToArray()), true, headers, CancellationToken.None);

            HorseQueue queue = ctx.Rider.Queue.Find("auto-topic-push");
            Assert.NotNull(queue);
            Assert.Equal("financial", queue.Topic);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithoutTopicHeader_QueueHasNoTopic(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            var producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            await producer.Queue.Push("no-topic-push", new MemoryStream("data"u8.ToArray()), true, CancellationToken.None);

            HorseQueue queue = ctx.Rider.Queue.Find("no-topic-push");
            Assert.NotNull(queue);
            Assert.Null(queue.Topic);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Topic Survives Message Push/Consume

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Topic_StaysAfterMessageDelivery(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("topic-stable-q");
            HorseQueue queue = ctx.Rider.Queue.Find("topic-stable-q");
            queue.Topic = "stable-topic";

            int received = 0;
            var consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
            await consumer.Queue.Subscribe("topic-stable-q", true, CancellationToken.None);

            var producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            for (int i = 0; i < 5; i++)
                await producer.Queue.Push("topic-stable-q", new MemoryStream(Encoding.UTF8.GetBytes($"msg-{i}")), true, CancellationToken.None);

            for (int i = 0; i < 30 && received < 5; i++)
                await Task.Delay(100);

            Assert.Equal(5, received);

            // Topic must remain unchanged after messages are delivered
            Assert.Equal("stable-topic", queue.Topic);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion

    #region Empty String Topic

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SetTopic_EmptyString_IsStoredAsEmpty(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            await ctx.Rider.Queue.Create("empty-topic-q", o => o.Type = QueueType.Push);
            HorseQueue queue = ctx.Rider.Queue.Find("empty-topic-q");

            queue.Topic = "";
            Assert.Equal("", queue.Topic);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    #endregion
}

