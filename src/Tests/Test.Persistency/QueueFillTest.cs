using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Test.Common;
using Test.Common.Models;
using Horse.Mq.Queues;
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

            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue route = server.Server.FindQueue("broadcast-a");
            HorseQueue push = server.Server.FindQueue("push-a");
            Assert.NotNull(route);
            Assert.NotNull(push);

            QueueFiller fillerRouteA = new QueueFiller(route);
            QueueFiller fillerPushA = new QueueFiller(push);

            fillerRouteA.FillJson(items, false, false);
            fillerPushA.FillJson(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(route.Messages);
            Assert.NotEmpty(push.Messages);
        }

        [Fact]
        public async Task FillString()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < 10; i++)
                items.Add("No #" + i);

            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillString(items, false, true);
            filler.FillString(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(queue.PriorityMessages);
            Assert.NotEmpty(queue.Messages);
        }

        [Fact]
        public async Task FillData()
        {
            List<byte[]> items = new List<byte[]>();
            for (int i = 0; i < 10; i++)
                items.Add(Encoding.UTF8.GetBytes("No #" + i));

            TestHorseMq server = new TestHorseMq();
            await server.Initialize();
            server.Start(300, 300);

            HorseQueue queue = server.Server.FindQueue("push-a");
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillData(items, false, true);
            filler.FillData(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(queue.PriorityMessages);
            Assert.NotEmpty(queue.Messages);
        }
    }
}