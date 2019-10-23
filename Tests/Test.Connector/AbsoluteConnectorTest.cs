using System;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.Server;
using Twino.Server.WebSockets;
using Xunit;

namespace Test.Connector
{
    public class AbsoluteConnectorTest
    {
        private readonly TwinoServer _server;
        private int _receivedMessages;

        public AbsoluteConnectorTest()
        {
            _server = TwinoServer.CreateWebSocket(async (twinoServer, request, client) =>
            {
                ServerSocket socket = new ServerSocket(twinoServer, request, client);
                socket.MessageReceived += (sender, message) => { _receivedMessages++; };
                socket.Send("Welcome");
                return await Task.FromResult(socket);
            });
        }

        [Fact]
        public void Connect()
        {
            _server.Start(431);
            System.Threading.Thread.Sleep(250);
            
            AbsoluteConnector connector = new AbsoluteConnector(TimeSpan.FromSeconds(1));
            connector.AddHost("ws://127.0.0.1:431");
            connector.Run();
            System.Threading.Thread.Sleep(1000);

            Assert.True(connector.IsConnected);

            SocketBase client = connector.GetClient();
            Assert.NotNull(client);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void DisconnectMultipleTimes()
        {
            int connectionCount = 0;
            
            _server.Start(422);
            AbsoluteConnector connector = new AbsoluteConnector(TimeSpan.FromMilliseconds(50));
            connector.AddHost("ws://127.0.0.1:422");
            connector.Connected += c => connectionCount++;
            
            connector.Run();
            System.Threading.Thread.Sleep(150);
            
            for (int i = 0; i < 19; i++)
            {
                connector.GetClient().Disconnect();
                System.Threading.Thread.Sleep(250);
            }

            Assert.Equal(20, connectionCount);
        }

        [Fact]
        public void SendData()
        {
            _server.Start(432);
            System.Threading.Thread.Sleep(250);
            
            AbsoluteConnector connector = new AbsoluteConnector(TimeSpan.FromSeconds(1));
            connector.AddHost("ws://127.0.0.1:432");
            connector.Run();
            System.Threading.Thread.Sleep(250);

            _receivedMessages = 0;
            connector.Send("Hello!");
            System.Threading.Thread.Sleep(1000);

            Assert.Equal(1, _receivedMessages);
        }

        [Fact]
        public void ReceiveData()
        {
            _server.Start(433);
            System.Threading.Thread.Sleep(250);
            int _receivedFromServer = 0;
            
            AbsoluteConnector connector = new AbsoluteConnector(TimeSpan.FromSeconds(1));
            connector.AddHost("ws://127.0.0.1:433");
            connector.MessageReceived += (client, message) => _receivedFromServer++; 
            connector.Run();
            
            System.Threading.Thread.Sleep(1000);
            Assert.Equal(1, _receivedFromServer);
        }

        [Fact]
        public void SendDataWhenOffline()
        {
            _server.Start(421);
            _receivedMessages = 0;

            AbsoluteConnector connector = new AbsoluteConnector(TimeSpan.FromMilliseconds(600));
            connector.AddHost("ws://127.0.0.1:421");
            
            connector.Run();
            System.Threading.Thread.Sleep(150);
            
            connector.Send("Message 1");
            
            for (int i = 0; i < 19; i++)
            {
                connector.GetClient().Disconnect();
                connector.Send("Message");

                while (!connector.IsConnected)
                    System.Threading.Thread.Sleep(50);
            }

            System.Threading.Thread.Sleep(3000);
            Assert.Equal(20, _receivedMessages);
        }
    }
}