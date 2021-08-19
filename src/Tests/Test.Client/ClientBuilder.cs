using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Routers;
using Microsoft.Extensions.DependencyInjection;
using Test.Common;
using Xunit;

namespace Test.Client
{
    public class ClientBuilder
    {
        [Fact]
        public async Task Builder()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClientBuilder b = new HorseClientBuilder();

            b.SetHost($"horse://localhost:{port}");

            b.AutoSubscribe(true);
            b.OnConnected(c => { });
            b.OnDisconnected(c => { });
            b.OnError(e => { });

            b.SetClientName("test-client");
            b.SetClientType("test-client");
            b.SetClientToken("1234567890");
            b.SetClientId("unique-id");

            b.AddSingletonConsumers(typeof(ServiceImplementation));
            b.AddSingletonHorseEvents(typeof(ServiceImplementation));
            b.AddSingletonDirectHandlers(typeof(ServiceImplementation));
            b.AddSingletonChannelSubscribers(typeof(ServiceImplementation));

            b.SetReconnectWait(TimeSpan.FromSeconds(5));

            HorseClient client = b.Build();
            await client.ConnectAsync();

            Assert.NotNull(client);
        }
    }
}