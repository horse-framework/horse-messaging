using System.Threading.Tasks;
using Test.Common;
using Twino.Client.TMQ;
using Twino.MQ.Queues;
using Twino.MQ.Routing;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Routers
{
    public class RouterTest
    {
        [Fact]
        public async Task Distribute()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 5, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "push-a-cc", 10, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 20, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 0, BindingInteraction.None));
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

            int client1Received = 0;
            int client2Received = 0;
            client1.MessageReceived += (c, m) => client1Received++;
            client2.MessageReceived += (c, m) => client2Received++;

            for (int i = 0; i < 4; i++)
            {
                TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
                Assert.Equal(TwinoResultCode.Ok, result.Code);
            }

            await Task.Delay(500);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");
            TwinoQueue queue2 = server.Server.FindQueue("push-a-cc");

            Assert.Equal(4, queue1.MessageCount());
            Assert.Equal(4, queue2.MessageCount());

            Assert.Equal(4, client2Received);
            Assert.Equal(4, client1Received);
        }

        [Fact]
        public async Task RoundRobin()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.RoundRobin);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 5, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "push-a-cc", 10, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 20, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 0, BindingInteraction.None));
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

            int client1Received = 0;
            int client2Received = 0;
            client1.MessageReceived += (c, m) => client1Received++;
            client2.MessageReceived += (c, m) => client2Received++;

            for (int i = 0; i < 5; i++)
            {
                TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
                Assert.Equal(TwinoResultCode.Ok, result.Code);
            }

            await Task.Delay(500);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");
            TwinoQueue queue2 = server.Server.FindQueue("push-a-cc");

            Assert.Equal(1, queue1.MessageCount());
            Assert.Equal(1, queue2.MessageCount());

            Assert.Equal(1, client2Received);
            Assert.Equal(2, client1Received);
        }

        [Fact]
        public async Task OnlyFirst()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.OnlyFirst);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 5, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "push-a-cc", 10, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 2, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 8, BindingInteraction.None));
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

            int client1Received = 0;
            int client2Received = 0;
            client1.MessageReceived += (c, m) => client1Received++;
            client2.MessageReceived += (c, m) => client2Received++;

            for (int i = 0; i < 4; i++)
            {
                TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
                Assert.Equal(TwinoResultCode.Ok, result.Code);
            }

            await Task.Delay(500);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");
            TwinoQueue queue2 = server.Server.FindQueue("push-a-cc");

            Assert.Equal(0, queue1.MessageCount());
            Assert.Equal(4, queue2.MessageCount());

            Assert.Equal(0, client1Received);
            Assert.Equal(0, client2Received);
        }

        [Fact]
        public async Task MultipleQueue()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 0, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "push-a-cc", 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");
            TwinoQueue queue2 = server.Server.FindQueue("push-a-cc");

            Assert.Equal(1, queue1.MessageCount());
            Assert.Equal(1, queue2.MessageCount());
        }

        [Fact]
        public async Task MultipleDirect()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 0, BindingInteraction.None));
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

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            Assert.True(client1Received);
            Assert.True(client2Received);
        }

        [Fact]
        public async Task MultipleOfflineDirect()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 0, BindingInteraction.None));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Failed, result.Code);
        }

        [Fact]
        public async Task SingleQueueSingleDirect()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.None));
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

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");

            Assert.Equal(1, queue1.MessageCount());
            Assert.True(client1Received);
        }

        [Fact]
        public async Task MultipleQueueMultipleDirect()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 0, BindingInteraction.None));
            router.AddBinding(new QueueBinding("qbind-2", "push-a-cc", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-2", "client-2", 0, BindingInteraction.None));
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

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);
            await Task.Delay(500);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");
            TwinoQueue queue2 = server.Server.FindQueue("push-a-cc");

            Assert.Equal(1, queue1.MessageCount());
            Assert.Equal(1, queue2.MessageCount());

            Assert.True(client1Received);
            Assert.True(client2Received);
        }

        [Fact]
        public async Task SingleQueueSingleDirectAckFromQueue()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);
            server.SendAcknowledgeFromMQ = true;

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 0, BindingInteraction.Response));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.None));
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

            TwinoQueue queue1 = server.Server.FindQueue("push-a");

            TwinoResult result = await producer.Routers.Publish("router", "Hello, World!", true);
            Assert.Equal(TwinoResultCode.Ok, result.Code);

            await Task.Delay(500);
            Assert.Equal(1, queue1.MessageCount());
            Assert.True(client1Received);
        }

        [Fact]
        public async Task SingleQueueSingleDirectResponseFromDirect()
        {
            TestTwinoMQ server = new TestTwinoMQ();
            await server.Initialize();
            int port = server.Start(300, 300);

            Router router = new Router(server.Server, "router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding("qbind-1", "push-a", 0, BindingInteraction.None));
            router.AddBinding(new DirectBinding("dbind-1", "client-1", 0, BindingInteraction.Response));
            server.Server.AddRouter(router);

            TmqClient producer = new TmqClient();
            await producer.ConnectAsync("tmq://localhost:" + port);
            Assert.True(producer.IsConnected);

            TmqClient client1 = new TmqClient();
            client1.ClientId = "client-1";
            await client1.ConnectAsync("tmq://localhost:" + port);
            client1.MessageReceived += (c, m) =>
            {
                TwinoMessage response = m.CreateResponse(TwinoResultCode.Ok);
                response.SetStringContent("Response");
                client1.SendAsync(response);
            };
            Assert.True(client1.IsConnected);

            TwinoQueue queue1 = server.Server.FindQueue("push-a");

            TwinoMessage message = await producer.Routers.PublishRequest("router", "Hello, World!");
            Assert.NotNull(message);
            Assert.Equal("Response", message.GetStringContent());
            Assert.Equal(1, queue1.MessageCount());
        }
    }
}