using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues;
using Test.Common;
using Test.Common.Models;
using Xunit;

namespace Test.Persistency;

public class QueueFillTest
{
    [Fact]
    public async Task FillJson()
    {
        List<QueueMessageA> items = new List<QueueMessageA>();
        for (int i = 0; i < 10; i++)
            items.Add(new QueueMessageA("No #" + i));

        await TestHorseRider.RunWith(async (server, _) =>
        {
            HorseQueue push = server.Rider.Queue.Find("push-a");
            Assert.NotNull(push);

            QueueFiller fillerPushA = new QueueFiller(push);
            fillerPushA.FillJson(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, push.Manager.MessageStore.Count());
        });
    }

    [Fact]
    public async Task FillString()
    {
        List<string> items = new List<string>();
        for (int i = 0; i < 10; i++)
            items.Add("No #" + i);

        await TestHorseRider.RunWith(async (server, _) =>
        {
            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillString(items, false, true);
            filler.FillString(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, queue.Manager.PriorityMessageStore.Count());
            Assert.NotEqual(0, queue.Manager.MessageStore.Count());
        });
    }

    [Fact]
    public async Task FillData()
    {
        List<byte[]> items = new List<byte[]>();
        for (int i = 0; i < 10; i++)
            items.Add(Encoding.UTF8.GetBytes("No #" + i));

        await TestHorseRider.RunWith(async (server, _) =>
        {
            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillData(items, false, true);
            filler.FillData(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, queue.Manager.PriorityMessageStore.Count());
            Assert.NotEqual(0, queue.Manager.MessageStore.Count());
        });
    }
}
