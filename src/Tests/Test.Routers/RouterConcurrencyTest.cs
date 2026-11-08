using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

/// <summary>
/// Records the message values it is called with
/// </summary>
public class RecordingBinding : Binding
{
    public string SeenTarget { get; private set; }
    public ushort SeenContentType { get; private set; }
    public MessageType SeenType { get; private set; }
    public bool SeenWaitResponse { get; private set; }

    public override Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        SeenTarget = message.Target;
        SeenContentType = message.ContentType;
        SeenType = message.Type;
        SeenWaitResponse = message.WaitResponse;
        return Task.FromResult(true);
    }
}

/// <summary>
/// Concurrent router and binding changes, and the smaller router fixes
/// </summary>
public class RouterConcurrencyTest
{
    private static HorseMessage CreateMessage(string routerName, ushort contentType = 0)
    {
        HorseMessage message = new HorseMessage(MessageType.Router, routerName, contentType);
        message.SetMessageId(Guid.NewGuid().ToString());
        message.SetStringContent("hello");
        return message;
    }

    [Fact]
    public async Task ConcurrentAddOfSameRouter_OnlyOneSucceeds()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = RouterPersistenceTest.UseNewFile(server);

            bool[] added = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                try
                {
                    server.Rider.Router.Add("race-router", RouteMethod.Distribute);
                    return true;
                }
                catch (DuplicateNameException)
                {
                    return false;
                }
            })));

            Assert.Single(added, x => x);
            Assert.Single(RouterPersistenceTest.ReadSaved(server, file), x => x.Name == "race-router");
        });
    }

    [Fact]
    public async Task RemoveThroughOldReference_KeepsNewerRouter()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = RouterPersistenceTest.UseNewFile(server);

            Router first = server.Rider.Router.Add("reused-router", RouteMethod.Distribute);
            server.Rider.Router.Remove(first);

            Router second = server.Rider.Router.Add("reused-router", RouteMethod.Distribute);
            second.AddBinding(RouterPersistenceTest.NewQueueBinding("q"));

            server.Rider.Router.Remove(first);

            Assert.Same(second, server.Rider.Router.Find("reused-router"));
            Assert.Single(Assert.Single(RouterPersistenceTest.ReadSaved(server, file), x => x.Name == "reused-router").Bindings);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ConcurrentBindingAdds_OnSameRouter_AreAllKept()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = RouterPersistenceTest.UseNewFile(server);
            Router router = server.Rider.Router.Add("busy-router", RouteMethod.Distribute);

            bool[] added = await Task.WhenAll(Enumerable.Range(0, 50)
                .Select(i => Task.Run(() => router.AddBinding(RouterPersistenceTest.NewQueueBinding($"b-{i}")))));

            Assert.All(added, Assert.True);
            Assert.Equal(50, router.Bindings.Length);
            Assert.Equal(50, Assert.Single(RouterPersistenceTest.ReadSaved(server, file), x => x.Name == "busy-router").Bindings.Count);
        });
    }

    /// <summary>
    /// A message that is being sent by a binding when the binding is removed still uses the binding's router
    /// </summary>
    [Fact]
    public async Task RemovedBinding_KeepsRouterForSendInProgress()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "inflight-router", RouteMethod.Distribute);
            QueueBinding binding = RouterPersistenceTest.NewQueueBinding("q");
            router.AddBinding(binding);
            server.Rider.Router.Add(router);

            router.RemoveBinding("q");
            Assert.Empty(router.Bindings);
            Assert.Same(router, binding.Router);

            Assert.True(await binding.Send(null, CreateMessage("inflight-router")));
        });
    }

    [Fact]
    public async Task AddBindingWithoutOptions_DoesNotThrow()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = server.Rider.Router.Add("no-options-router", RouteMethod.Distribute);
            router.AddBinding<QueueBinding>();
            Assert.Single(router.Bindings);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task IsEnabled_IsSavedAndLoadedAfterRestart()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            string file = RouterPersistenceTest.UseNewFile(server);
            Router router = server.Rider.Router.Add("toggle-router", RouteMethod.Distribute);

            router.IsEnabled = false;
            Assert.False(RouterPersistenceTest.ReadSaved(server, file).Single(x => x.Name == "toggle-router").IsEnabled);

            HorseRider restarted = RouterPersistenceTest.Restart(server, file);
            Assert.False(restarted.Router.Find("toggle-router").IsEnabled);
            Assert.Equal(RouterPublishResult.Disabled, await restarted.Router.Find("toggle-router").Publish(null, CreateMessage("toggle-router")));
        });
    }

    [Fact]
    public async Task MissingInteraction_IsLoadedAsNone()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Binding binding = server.Rider.Router.CreateBinding(new BindingConfiguration
            {
                Name = "q",
                Type = typeof(QueueBinding).FullName,
                Target = "push-a",
                Interaction = null
            });

            Assert.NotNull(binding);
            Assert.Equal(BindingInteraction.None, binding.Interaction);
            await Task.CompletedTask;
        });
    }

    /// <summary>
    /// A direct binding changes the target, type, content type and acknowledge flag of the message it sends.
    /// The bindings after it get the message as the producer sent it.
    /// </summary>
    [Fact]
    public async Task LaterBindings_GetMessageAsProducerSentIt()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient receiver = new HorseClient { ClientId = "client-1" };
            await receiver.ConnectAsync("horse://localhost:" + port);
            Assert.True(receiver.IsConnected);

            Router router = new Router(server.Rider, "state-router", RouteMethod.Distribute);
            router.AddBinding(new DirectBinding { Name = "direct", Target = "client-1", ContentType = 777, Priority = 10, Interaction = BindingInteraction.Response });
            RecordingBinding recorder = new RecordingBinding { Name = "recorder", Priority = 5 };
            router.AddBinding(recorder);
            router.AddBinding(new QueueBinding { Name = "queue", Target = "state-queue", Priority = 0, Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            HorseMessage message = CreateMessage("state-router", 5);
            message.WaitResponse = false;
            await router.Publish(null, message);

            Assert.Equal("state-router", recorder.SeenTarget);
            Assert.Equal((ushort)5, recorder.SeenContentType);
            Assert.Equal(MessageType.Router, recorder.SeenType);
            Assert.False(recorder.SeenWaitResponse);

            HorseQueue queue = server.Rider.Queue.Find("state-queue");
            Assert.NotNull(queue);
            Assert.Equal((ushort)5, queue.Manager.MessageStore.ReadFirst().Message.ContentType);
        });
    }

    [Fact]
    public async Task ClientAddBinding_TypeChecksAndRepeatedRequests()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { ResponseTimeout = TimeSpan.FromSeconds(5) };
            await client.ConnectAsync("horse://localhost:" + port);
            Assert.True(client.IsConnected);

            Assert.Equal(HorseResultCode.Ok, (await client.Router.Create("client-router", RouteMethod.Distribute)).Code);

            Task<HorseResult> Add(string type, string name, string target) =>
                client.Router.AddBinding("client-router", type, name, target, BindingInteraction.None, RouteMethod.Distribute, null, 0);

            Assert.Equal(HorseResultCode.Ok, (await Add("QueueBinding", "q", "push-a")).Code);

            // the same binding again, as a service does at every start
            Assert.Equal(HorseResultCode.Ok, (await Add("QueueBinding", "q", "push-a")).Code);

            // another binding with the same name
            Assert.Equal(HorseResultCode.Duplicate, (await Add("QueueBinding", "q", "push-a-cc")).Code);

            // types that are not bindings are not created
            Assert.Equal(HorseResultCode.NotFound, (await Add("System.Text.StringBuilder", "sb", "x")).Code);
            Assert.Equal(HorseResultCode.NotFound, (await Add("Binding", "abstract", "x")).Code);

            Router router = server.Rider.Router.Find("client-router");
            Assert.Equal("push-a", Assert.Single(router.Bindings).Target);
        });
    }

    [Fact]
    public async Task ConcurrentClientCreatesOfSameRouter_AllGetOk()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient[] clients = new HorseClient[8];
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i] = new HorseClient { ResponseTimeout = TimeSpan.FromSeconds(5) };
                await clients[i].ConnectAsync("horse://localhost:" + port);
                Assert.True(clients[i].IsConnected);
            }

            HorseResult[] results = await Task.WhenAll(clients.Select(c => c.Router.Create("shared-router", RouteMethod.Distribute)));

            Assert.All(results, r => Assert.Equal(HorseResultCode.Ok, r.Code));
            Assert.NotNull(server.Rider.Router.Find("shared-router"));
        });
    }
}
