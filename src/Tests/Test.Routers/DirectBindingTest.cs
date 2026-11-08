using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

public class DirectBindingTest
{
    /// <summary>
    /// Accepts only clients whose name starts with "accepted"
    /// </summary>
    public class AcceptedClientsBinding : DirectBinding
    {
        public AcceptedClientsBinding()
        {
            SetClientFilter(c => c.Name != null && c.Name.StartsWith("accepted", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Sets a filter for each message from its "To" header, like the sample custom binding
    /// </summary>
    public class HeaderFilterBinding : DirectBinding
    {
        public override Task<bool> Send(MessagingClient sender, HorseMessage message)
        {
            string to = message.FindHeader("To");
            SetClientFilter(c => c.Name == to);
            return base.Send(sender, message);
        }
    }

    private sealed class Receiver
    {
        private int _count;
        public HorseClient Client { get; }
        public int Count => Volatile.Read(ref _count);

        public Receiver(HorseClient client)
        {
            Client = client;
            client.MessageReceived += (_, m) =>
            {
                if (m.Type == MessageType.DirectMessage)
                    Interlocked.Increment(ref _count);
            };
        }
    }

    private static async Task<Receiver> Connect(int port, string name, string type)
    {
        HorseClient client = new HorseClient();
        client.SetClientName(name);
        client.SetClientType(type);
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return new Receiver(client);
    }

    private static HorseMessage CreateMessage(string to = null)
    {
        HorseMessage message = new HorseMessage(MessageType.Router, "direct-router", 0);
        message.SetMessageId(Guid.NewGuid().ToString());
        if (to != null)
            message.AddHeader("To", to);

        message.SetStringContent("hello");
        return message;
    }

    private static Router CreateRouter(TestHorseRider server, Binding binding)
    {
        Router router = new Router(server.Rider, "direct-router", RouteMethod.Distribute);
        router.AddBinding(binding);
        server.Rider.Router.Add(router);
        return router;
    }

    [Fact]
    public async Task RoundRobin_SendsToClientsInTurn()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Receiver r1 = await Connect(port, "worker-1", "worker");
            Receiver r2 = await Connect(port, "worker-2", "worker");
            Router router = CreateRouter(server, new DirectBinding
            {
                Name = "direct", Target = "@type:worker", Interaction = BindingInteraction.None, RouteMethod = RouteMethod.RoundRobin
            });

            for (int i = 0; i < 6; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            await TestHorseRider.WaitUntil(() => r1.Count + r2.Count == 6);
            Assert.Equal(3, r1.Count);
            Assert.Equal(3, r2.Count);
        });
    }

    [Fact]
    public async Task ClientFilter_SendsOnlyToAcceptedClients()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Receiver accepted1 = await Connect(port, "accepted-1", "worker");
            Receiver rejected = await Connect(port, "rejected", "worker");
            Receiver accepted2 = await Connect(port, "accepted-2", "worker");
            Router router = CreateRouter(server, new AcceptedClientsBinding
            {
                Name = "direct", Target = "@type:worker", Interaction = BindingInteraction.None, RouteMethod = RouteMethod.Distribute
            });

            for (int i = 0; i < 2; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            await TestHorseRider.WaitUntil(() => accepted1.Count + accepted2.Count == 4);
            await Task.Delay(100);
            Assert.Equal(2, accepted1.Count);
            Assert.Equal(2, accepted2.Count);
            Assert.Equal(0, rejected.Count);
        });
    }

    /// <summary>
    /// A filter set for each message must be applied to that message, not cached from an earlier one
    /// </summary>
    [Fact]
    public async Task ClientFilter_SetPerMessage_IsAppliedToEachMessage()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Receiver a = await Connect(port, "a", "worker");
            Receiver b = await Connect(port, "b", "worker");
            Router router = CreateRouter(server, new HeaderFilterBinding
            {
                Name = "direct", Target = "@type:worker", Interaction = BindingInteraction.None, RouteMethod = RouteMethod.Distribute
            });

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("a")));
            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("b")));
            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("b")));
            Assert.Equal(RouterPublishResult.NoReceivers, await router.Publish(null, CreateMessage("nobody")));

            await TestHorseRider.WaitUntil(() => a.Count + b.Count == 3);
            await Task.Delay(100);
            Assert.Equal(1, a.Count);
            Assert.Equal(2, b.Count);
        });
    }
}
