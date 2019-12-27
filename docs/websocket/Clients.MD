## WebSocket Client Management

We discussed how to recive and send message over websocket in [Introducing to Twino WebSockets](https://github.com/mhelvacikoylu/twino/blob/v2/docs/websocket/Introduce.MD) article.
However, receiving and sending messages over websocket is not a complete solution when you need to manage your clients or just do something more complex.

Twino has two kind of solution about client management.

First is the basic way. Your method is called when a client is connected.
HTTP data includes in connection data; method, path and all header key and values in properties property.

Second is advanced way. You need to implement an interface: **IProtocolConnectionHandler<WsServerSocket, WebSocketMessage>**.
In this way, you can decide if connection will be accepted or not.
What type will your socket be. You can do something before handshaking or after.
You will be notified when a client is disconnected from server.
And of course, you can handle incoming messages within.

Here is the first and basic solution:

            TwinoServer server = new TwinoServer();

            server.UseWebSockets((socket, data) =>
                                 {
                                     Console.WriteLine("Client connected");
                                     return Task.CompletedTask;
                                 },
                                 async (socket, message) =>
                                 {
                                     Console.WriteLine($"# {message}");
                                     await socket.SendAsync(message);
                                 });
            
            server.Start(80);
            server.BlockWhileRunning();
            
We have one more parameter (first parameter) in UseWebSockets method.
It's called when a client is connected.
And incoming messages will be handled in second parameter action.

And here is the second way

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
            server.UseWebSockets(handler);
            
            server.Start(80);
            server.BlockWhileRunning();
        }
    }
