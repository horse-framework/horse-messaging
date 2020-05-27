using System;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.MQ.Routing;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq
{
    /// <summary>
    /// Ports 42900 - 42950
    /// </summary>
    public class RouterTest
    {
        [Fact]
        public async Task Distribute()
        {
            int port = 42901;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            throw new NotImplementedException();
        }

        [Fact]
        public async Task RoundRobin()
        {
            int port = 42902;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            throw new NotImplementedException();
        }

        [Fact]
        public async Task OnlyFirst()
        {
            int port = 42903;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            throw new NotImplementedException();
        }

        [Fact]
        public async Task MultipleQueue()
        {
            int port = 42904;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "ch-push-cc", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, result.Code);

            Channel channel1 = server.Server.FindChannel("ch-push");
            Channel channel2 = server.Server.FindChannel("ch-push-cc");

            ChannelQueue queue1 = channel1.FindQueue(MessageA.ContentType);
            ChannelQueue queue2 = channel2.FindQueue(MessageA.ContentType);

            Assert.Equal(1, queue1.MessageCount());
            Assert.Equal(1, queue2.MessageCount());
        }

        [Fact]
        public async Task MultipleDirect()
        {
            int port = 42905;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client1.IsConnected);

            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client2.IsConnected);

            bool client1Received = false;
            bool client2Received = false;
            client1.MessageReceived += (c, m) => client1Received = true;
            client2.MessageReceived += (c, m) => client2Received = true;

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            Assert.True(client1Received);
            Assert.True(client2Received);
        }

        [Fact]
        public async Task MultipleOfflineDirect()
        {
            int port = 42906;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Failed, result.Code);
        }

        [Fact]
        public async Task SingleQueueSingleDirect()
        {
            int port = 42907;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            bool client1Received = false;
            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            client1.MessageReceived += (c, m) => client1Received = true;
            Assert.True(client1.IsConnected);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            Channel channel1 = server.Server.FindChannel("ch-push");
            ChannelQueue queue1 = channel1.FindQueue(MessageA.ContentType);

            Assert.Equal(1, queue1.MessageCount());
            Assert.True(client1Received);
        }

        [Fact]
        public async Task MultipleQueueMultipleDirect()
        {
            int port = 42908;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "ch-push-cc", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client1.IsConnected);

            TmqClient client2 = new TmqClient();
            client2.ClientId = "client-2";
            await client2.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client2.IsConnected);

            bool client1Received = false;
            bool client2Received = false;
            client1.MessageReceived += (c, m) => client1Received = true;
            client2.MessageReceived += (c, m) => client2Received = true;

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            Channel channel1 = server.Server.FindChannel("ch-push");
            Channel channel2 = server.Server.FindChannel("ch-push-cc");

            ChannelQueue queue1 = channel1.FindQueue(MessageA.ContentType);
            ChannelQueue queue2 = channel2.FindQueue(MessageA.ContentType);

            Assert.Equal(1, queue1.MessageCount());
            Assert.Equal(1, queue2.MessageCount());

            Assert.True(client1Received);
            Assert.True(client2Received);
        }

        [Fact]
        public async Task SingleQueueSingleDirectAckFromQueue()
        {
            int port = 42909;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);
            server.SendAcknowledgeFromMQ = true;

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.Acknowledge));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            bool client1Received = false;
            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            client1.MessageReceived += (c, m) => client1Received = true;
            Assert.True(client1.IsConnected);

            Channel channel1 = server.Server.FindChannel("ch-push");
            ChannelQueue queue1 = channel1.FindQueue(MessageA.ContentType);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true, MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, result.Code);

            await Task.Delay(500);
            Assert.Equal(1, queue1.MessageCount());
            Assert.True(client1Received);
        }

        [Fact]
        public async Task SingleQueueSingleDirectResponseFromDirect()
        {
            int port = 42910;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "ch-push", MessageA.ContentType, 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", MessageA.ContentType, 0, BindingInteraction.Response));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            client1.MessageReceived += (c, m) =>
            {
                TmqMessage response = m.CreateResponse(TwinoResultCode.Ok);
                response.SetStringContent("Response");
                client1.SendAsync(response);
            };
            Assert.True(client1.IsConnected);

            Channel channel1 = server.Server.FindChannel("ch-push");
            ChannelQueue queue1 = channel1.FindQueue(MessageA.ContentType);

            TmqMessage message = await producer.Routers.PublishRequest("router", MessageA.ContentType, "Hello, World!");
            Assert.NotNull(message);
            Assert.Equal("Response", message.GetStringContent());
            Assert.Equal(1, queue1.MessageCount());
        }
    }
}