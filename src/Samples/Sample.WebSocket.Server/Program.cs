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
    public class ServerWsHandler : IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>
    {
        public async Task<WsServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            WsServerSocket socket = new WsServerSocket(server, connection);
            Program.ServerClient = socket;
            Console.WriteLine("> Connected");

            return await Task.FromResult(socket);
        }

        public async Task Ready(ITwinoServer server, WsServerSocket client)
        {
            await Task.CompletedTask;
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
        {
            Console.WriteLine(message);
            await Task.CompletedTask;
        }

        public async Task Disconnected(ITwinoServer server, WsServerSocket client)
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