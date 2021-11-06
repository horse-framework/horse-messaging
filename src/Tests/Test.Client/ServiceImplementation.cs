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
    public class ServiceImplementation
    {
        [Fact]
        public async Task Service()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);
            
            IServiceCollection services = new ServiceCollection();

            services.AddHorseBus(b =>
            {
                b.AddHost($"horse://localhost:{port}");
                
                b.AutoSubscribe(true);
                b.OnConnected(c => { });
                b.OnDisconnected(c => { });
                b.OnError(e => { });

                b.SetClientName("test-client");
                b.SetClientType("test-client");
                b.SetClientToken("1234567890");
                b.SetClientId("unique-id");

                b.AddTransientConsumers(typeof(ServiceImplementation));
                b.AddTransientHorseEvents(typeof(ServiceImplementation));
                b.AddTransientDirectHandlers(typeof(ServiceImplementation));
                b.AddTransientChannelSubscribers(typeof(ServiceImplementation));

                b.SetReconnectWait(TimeSpan.FromSeconds(5));
            });

            IServiceProvider provider = services.BuildServiceProvider();
            provider.UseHorseBus();

            HorseClient client = provider.GetService<HorseClient>();
            IHorseCache cache = provider.GetService<IHorseCache>();
            IHorseQueueBus queue = provider.GetService<IHorseQueueBus>();
            IHorseChannelBus channel = provider.GetService<IHorseChannelBus>();
            IHorseDirectBus direct = provider.GetService<IHorseDirectBus>();
            IHorseRouterBus router = provider.GetService<IHorseRouterBus>();

            Assert.NotNull(client);
            Assert.NotNull(cache);
            Assert.NotNull(queue);
            Assert.NotNull(channel);
            Assert.NotNull(direct);
            Assert.NotNull(router);
            server.Stop();
        }
    }
}