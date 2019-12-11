using Twino.Server;
using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.Protocols.WebSocket;

namespace Sample.WebSocket.Server
{
    /// <summary>
    /// WebSocket Client Factory
    /// </summary>
    public class ServerWsHandler : IProtocolConnectionHandler<WebSocketMessage>
    {
        public async Task<SocketBase> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            WsServerSocket socket = new WsServerSocket(server, connection);
            Program.ServerClient = socket;
            Console.WriteLine("> Connected");

            return await Task.FromResult(socket);
        }

        public async Task Ready(ITwinoServer server, SocketBase client)
        {
            await Task.CompletedTask;
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, SocketBase client, WebSocketMessage message)
        {
            Console.WriteLine(message);
            await Task.CompletedTask;
        }

        public async Task Disconnected(ITwinoServer server, SocketBase client)
        {
            Console.WriteLine("> Disconnected");
            await Task.CompletedTask;
        }
    }

    class Program
    {
        public static WsServerSocket ServerClient { get; set; }

        static void Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            server.UseWebSockets(new ServerWsHandler());

            server.Start(82);

            while (true)
            {
                string msg = Console.ReadLine();
                ServerClient.Send(msg);
            }
        }
    }
}