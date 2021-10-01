using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Test.Common;
using Test.Events.Handlers.Connection;
using Xunit;

namespace Test.Events
{
    public class ConnectionEventTest
    {
        [Fact]
        public async Task ClientConnect()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ClientConnectHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseClient client2 = new HorseClient();
            await client2.ConnectAsync($"horse://localhost:{port}");
            
            await Task.Delay(250);
            Assert.Equal(1, ClientConnectHandler.Count);
            server.Stop();
        }
        
        
        [Fact]
        public async Task ClientDisconnect()
        {
            TestHorseRider server = new TestHorseRider();
            await server.Initialize();
            int port = server.Start(300, 300);

            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<ClientDisconnectHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseClient client2 = new HorseClient();
            await client2.ConnectAsync($"horse://localhost:{port}");
            
            await Task.Delay(250);
            Assert.Equal(0, ClientDisconnectHandler.Count);

            client2.Disconnect();
            
            await Task.Delay(250);
            Assert.Equal(1, ClientDisconnectHandler.Count);
            server.Stop();
        }
    }
}