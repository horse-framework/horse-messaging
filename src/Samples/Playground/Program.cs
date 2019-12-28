using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.Http;
using Twino.Protocols.TMQ;
using Twino.Protocols.WebSocket;
using Twino.Server;
using Xunit;

namespace Playground
{
    public class SampleWebSocketHandler : IProtocolConnectionHandler<WsServerSocket,WebSocketMessage>
    {
        public async Task<WsServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            Console.WriteLine("Client connected");
            WsServerSocket socket = new WsServerSocket(server, connection);
            return await Task.FromResult(socket);
        }

        public Task Ready(ITwinoServer server, WsServerSocket client)
        {
            Console.WriteLine("Client is ready");
            return Task.CompletedTask;
        }

        public async Task Received(ITwinoServer server, IConnectionInfo info, WsServerSocket client, WebSocketMessage message)
        {
            Console.WriteLine($"# {message}");
            await client.SendAsync(message);
        }

        public Task Disconnected(ITwinoServer server, WsServerSocket client)
        {
            Console.WriteLine("Client disconnected");
            return Task.CompletedTask;
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = new TwinoServer();

            SampleWebSocketHandler handler = new SampleWebSocketHandler();
            HttpOptions options = new HttpOptions();
            
            server.UseWebSockets(handler);
            
            server.Start(80);
            server.BlockWhileRunning();
        }
    }
}