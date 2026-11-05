using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Xunit;

namespace Test.Queues.Options;

public class QueueLimitTest
{
    private static HorseRider CreateRider(string dataPath, int queueLimit)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o =>
            {
                o.DataPath = dataPath;
                o.QueueLimit = queueLimit;
            })
            .ConfigureQueues(q => q.UseMemoryQueues())
            .Build();
    }

    private static void Delete(string dataPath)
    {
        try
        {
            if (Directory.Exists(dataPath))
                Directory.Delete(dataPath, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task QueueLimit_AllowsQueuesUpToLimit()
    {
        string dataPath = $"ql-{Guid.NewGuid():N}";
        HorseRider rider = CreateRider(dataPath, 2);

        try
        {
            await rider.Queue.Create("limit-1");
            await rider.Queue.Create("limit-2");

            await Assert.ThrowsAsync<OperationCanceledException>(() => rider.Queue.Create("limit-3"));
            Assert.Equal(2, rider.Queue.Queues.Count());

            //an existing queue is still returned when the server is at its limit
            HorseQueue existing = await rider.Queue.Create("limit-1", QueueOptions.CloneFrom(rider.Queue.Options), null, false, true);
            Assert.Same(rider.Queue.Find("limit-1"), existing);
        }
        finally
        {
            Delete(dataPath);
        }
    }

    [Fact]
    public async Task QueueLimit_Zero_IsUnlimited()
    {
        string dataPath = $"ql-{Guid.NewGuid():N}";
        HorseRider rider = CreateRider(dataPath, 0);

        try
        {
            for (int i = 0; i < 5; i++)
                await rider.Queue.Create($"unlimited-{i}");

            Assert.Equal(5, rider.Queue.Queues.Count());
        }
        finally
        {
            Delete(dataPath);
        }
    }
}
