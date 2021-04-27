using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Test.Common;
using Test.Events.Handlers.Direct;
using Xunit;

namespace Test.Events
{
    public class DirectEventTest
    {
        [Fact]
        public async Task DirectMessage()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();
            client.SetClientName("test-client");

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<DirectMessageHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Event.Subscribe(HorseEventType.DirectMessage, null, true);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            HorseResult directResult = await client.Direct.SendByName("test-client", 0, new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!")), false);
            Assert.Equal(HorseResultCode.Ok, directResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, DirectMessageHandler.Count);
        }

        [Fact]
        public async Task ResponseMessage()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();
            client.SetClientName("test-client");
            client.MessageReceived += (c, m) => { c.SendAsync(m.CreateResponse(HorseResultCode.Ok)); };

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<DirectResponseHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Event.Subscribe(HorseEventType.DirectMessageResponse, null, true);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            await client.Direct.Request("@name:test-client", 0, new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!")));

            await Task.Delay(250);
            Assert.Equal(1, DirectResponseHandler.Count);
        }
    }
}