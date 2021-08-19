using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Test.Common;
using Test.Common.Models;
using Horse.Messaging.Server.Queues;
using Xunit;

namespace Test.Persistency
{
    public class QueueFillTest
    {
        [Fact]
        public async Task FillJson()
        {
            List<QueueMessageA> items = new List<QueueMessageA>();
            for (int i = 0; i < 10; i++)
                items.Add(new QueueMessageA("No #" + i));

            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue push = server.Rider.Queue.Find("push-a");
            Assert.NotNull(push);

            QueueFiller fillerPushA = new QueueFiller(push);

            fillerPushA.FillJson(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, push.MessageCount());
        }

        [Fact]
        public async Task FillString()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < 10; i++)
                items.Add("No #" + i);

            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillString(items, false, true);
            filler.FillString(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, queue.PriorityMessageCount());
            Assert.NotEqual(0, queue.MessageCount());
        }

        [Fact]
        public async Task FillData()
        {
            List<byte[]> items = new List<byte[]>();
            for (int i = 0; i < 10; i++)
                items.Add(Encoding.UTF8.GetBytes("No #" + i));

            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue queue = server.Rider.Queue.Find("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillData(items, false, true);
            filler.FillData(items, false, false);

            await Task.Delay(500);
            Assert.NotEqual(0, queue.PriorityMessageCount());
            Assert.NotEqual(0, queue.MessageCount());
        }
    }
}