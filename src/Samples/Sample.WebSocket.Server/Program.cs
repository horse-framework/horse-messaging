using Twino.Server;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private int _online;

        public int Online => _online;

        public async Task<WsServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            WsServerSocket socket = new WsServerSocket(server, connection);
            Interlocked.Increment(ref _online);
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
            Interlocked.Decrement(ref _online);
            await Task.CompletedTask;
        }
    }

    class Program
    {
        public static WsServerSocket ServerClient { get; set; }

        static void Main(string[] args)
        {
            ServerWsHandler handler = new ServerWsHandler();
            TwinoServer server = new TwinoServer(new ServerOptions
                                                 {
                                                     PingInterval = 8,
                                                     Hosts = new List<HostOptions>
                                                             {
                                                                 new HostOptions
                                                                 {
                                                                     Port = 83
                                                                 }
                                                             }
                                                 });
            server.UseWebSockets(handler);

            server.Start();

            while (true)
            {
                Console.ReadLine();
                Console.WriteLine(handler.Online + " Online");
            }
        }
    }
}