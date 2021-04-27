using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Test.Common;
using Test.Events.Handlers.Router;
using Xunit;

namespace Test.Events
{
    public class RouterEventTest
    {
        [Fact]
        public async Task RouterCreate()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<RouterCreateHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Router.Create("test-router", RouteMethod.Distribute);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, RouterCreateHandler.Count);
        }

        [Fact]
        public async Task RouterRemove()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<RouterRemoveHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Router.Create("test-router", RouteMethod.Distribute);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, RouterRemoveHandler.Count);

            HorseResult removeResult = await client.Router.Remove("test-router");
            Assert.Equal(HorseResultCode.Ok, removeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, RouterRemoveHandler.Count);
        }

        [Fact]
        public async Task BindingAdd()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<AddBindingHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Router.Create("test-router", RouteMethod.Distribute);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            HorseResult bindingResult = await client.Router.AddBinding("test-router", BindingType.Direct, "binding-1", "@name:client-test", BindingInteraction.None);
            Assert.Equal(HorseResultCode.Ok, bindingResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, AddBindingHandler.Count);
        }

        [Fact]
        public async Task BindingRemove()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<RemoveBindingHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Router.Create("test-router", RouteMethod.Distribute);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            HorseResult bindingResult = await client.Router.AddBinding("test-router", BindingType.Direct, "binding-1", "@name:client-test", BindingInteraction.None);
            Assert.Equal(HorseResultCode.Ok, bindingResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, RemoveBindingHandler.Count);

            HorseResult removeResult = await client.Router.RemoveBinding("test-router", "binding-1");
            Assert.Equal(HorseResultCode.Ok, removeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, RemoveBindingHandler.Count);
        }

        [Fact]
        public async Task Publish()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<RouterPublishHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Router.Create("router", RouteMethod.Distribute);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);
            
            HorseResult result = await client.Event.Subscribe(HorseEventType.RouterPublish, "router", true);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            await client.Router.Publish("router", "Hello, World!", false);

            await Task.Delay(250);
            Assert.Equal(1, RouterPublishHandler.Count);
        }
    }
}