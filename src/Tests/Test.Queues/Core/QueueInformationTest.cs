using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Core;

public class QueueInformationTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ListQueues_Unacknowledges_ExposesAckTimeoutCount(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("info-unack", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.No;
            o.AcknowledgeTimeout = System.TimeSpan.FromSeconds(2);
        });

        var consumer = new HorseClient
        {
            AutoAcknowledge = false
        };

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("info-unack", true, CancellationToken.None);

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("info-unack", new MemoryStream("unack"u8.ToArray()), false, CancellationToken.None);

        await Task.Delay(4000);

        HorseModelResult<List<QueueInformation>> result = await producer.Queue.List(CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, result.Result.Code);

        QueueInformation info = result.Model.FirstOrDefault(q => q.Name == "info-unack");
        Assert.NotNull(info);
        Assert.Equal(1, info.Unacknowledges);
        Assert.Equal(0, info.NegativeAcks);

        producer.Disconnect();
        consumer.Disconnect();
    }
}
