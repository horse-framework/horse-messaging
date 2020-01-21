using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.MQ;
using Twino.MQ.Queues;
using Xunit;

namespace Test.Mq
{
    public class QueueFillTest
    {
        [Fact]
        public async Task FillJson()
        {
            List<MessageA> items = new List<MessageA>();
            for (int i = 0; i < 10; i++)
                items.Add(new MessageA("No #" + i));

            TestMqServer server = new TestMqServer();
            server.Initialize(47701);
            server.Start(300, 300);

            Channel route = server.Server.FindChannel("ch-route");
            Channel push = server.Server.FindChannel("ch-push");
            Assert.NotNull(route);
            Assert.NotNull(push);

            ChannelQueue routeA = route.FindQueue(MessageA.ContentType);
            ChannelQueue pushA = push.FindQueue(MessageA.ContentType);
            Assert.NotNull(routeA);
            Assert.NotNull(pushA);

            QueueFiller fillerRouteA = new QueueFiller(routeA);
            QueueFiller fillerPushA = new QueueFiller(pushA);
            
            await fillerRouteA.FillJson(items, false, false);
            await fillerPushA.FillJson(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(routeA.RegularMessages);
            Assert.NotEmpty(pushA.RegularMessages);
        }

        [Fact]
        public async Task FillString()
        {
            List<string> items = new List<string>();
            for (int i = 0; i < 10; i++)
                items.Add("No #" + i);

            TestMqServer server = new TestMqServer();
            server.Initialize(39702);
            server.Start(300, 300);

            Channel channel = server.Server.FindChannel("ch-push");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillString(items, false, true);
            filler.FillString(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(queue.HighPriorityMessages);
            Assert.NotEmpty(queue.RegularMessages);
        }

        [Fact]
        public async Task FillData()
        {
            List<byte[]> items = new List<byte[]>();
            for (int i = 0; i < 10; i++)
                items.Add(Encoding.UTF8.GetBytes("No #" + i));

            TestMqServer server = new TestMqServer();
            server.Initialize(40702);
            server.Start(300, 300);

            Channel channel = server.Server.FindChannel("ch-push");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            QueueFiller filler = new QueueFiller(queue);
            filler.FillData(items, false, true);
            filler.FillData(items, false, false);

            await Task.Delay(500);
            Assert.NotEmpty(queue.HighPriorityMessages);
            Assert.NotEmpty(queue.RegularMessages);
        }
    }
}