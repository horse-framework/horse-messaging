using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.WebSocket;
using Twino.Client.WebSocket.Connectors;
using Twino.Core;
using Twino.Protocols.WebSocket;
using Twino.Server;
using Xunit;

namespace Test.Connector
{
    public class AbsoluteConnectorTest
    {
        private readonly TwinoServer _server;
        private int _receivedMessages;

        public AbsoluteConnectorTest()
        {
            _server = new TwinoServer(ServerOptions.CreateDefault());
            _server.UseWebSockets(async (socket) => { await socket.SendAsync("Welcome"); },
                                  async (socket, message) =>
                                  {
                                      _receivedMessages++;
                                      await Task.CompletedTask;
                                  });
        }

        [Fact]
        public async Task Connect()
        {
            _server.Start(46431);
            await Task.Delay(1250);

            WsAbsoluteConnector connector = new WsAbsoluteConnector(TimeSpan.FromMilliseconds(500));
            connector.AddHost("ws://127.0.0.1:46431");
            connector.Run();
            await Task.Delay(1250);

            Assert.True(connector.IsConnected);

            SocketBase client = connector.GetClient();
            Assert.NotNull(client);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public void DisconnectMultipleTimes()
        {
            int connectionCount = 0;

            _server.Start(46422);
            WsAbsoluteConnector connector = new WsAbsoluteConnector(TimeSpan.FromMilliseconds(50));
            connector.AddHost("ws://127.0.0.1:46422");
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
            _server.Start(46432);
            System.Threading.Thread.Sleep(250);

            WsAbsoluteConnector connector = new WsAbsoluteConnector(TimeSpan.FromSeconds(1));
            connector.AddHost("ws://127.0.0.1:46432");
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
            _server.Start(46433);
            System.Threading.Thread.Sleep(250);
            int _receivedFromServer = 0;

            WsAbsoluteConnector connector = new WsAbsoluteConnector(TimeSpan.FromSeconds(1));
            connector.AddHost("ws://127.0.0.1:46433");
            connector.MessageReceived += (client, message) => _receivedFromServer++;
            connector.Run();

            System.Threading.Thread.Sleep(1000);
            Assert.Equal(1, _receivedFromServer);
        }

        [Fact]
        public void SendDataWhenOffline()
        {
            _server.Start(46421);
            _receivedMessages = 0;

            WsAbsoluteConnector connector = new WsAbsoluteConnector(TimeSpan.FromMilliseconds(600));
            connector.AddHost("ws://127.0.0.1:46421");

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