using System;
using System.Threading.Tasks;
using Twino.Server;
using Twino.Server.WebSockets;
using Xunit;

namespace Test.Connector
{
    public class StickyConnectorTest
    {
        private readonly TwinoServer _server;
        private int _receivedMessages;

        public StickyConnectorTest()
        {
            _server = TwinoServer.CreateWebSocket(async (twinoServer, request, client) =>
            {
                ServerSocket socket = new ServerSocket(twinoServer, request, client);
                socket.MessageReceived += (sender, message) => { _receivedMessages++; };
                return await Task.FromResult(socket);
            });
        }

        [Fact]
        public void SendDataWhenOffline()
        {
            throw new NotImplementedException();
        }

    }
}