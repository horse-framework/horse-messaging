## Introducing to Twino WebSockets

WebSocket protocol is a TCP based protocol. But it requires to HTTP protocol for handshaking.
WebSocket protocol handshaking is interpreted by HTTP protocol.

If you need to create and WebSocket server, you have to create an http protocol.
Otherwise, you can't handshake incoming connections.

Twino WebSocket creates an HTTP server while creating websocket server, if you did not create already.
To see how it works, create an example.

You can test that sample code by connection from [websocket.org](http://www.websocket.org/echo.html).
Be sure, you visited HTTP version of websocket.org, not HTTPS. Because sample is accepting non-ssl connections.

            TwinoServer server = new TwinoServer();

            server.UseWebSockets(async (socket, message) =>
            {
                Console.WriteLine($"# {message}");
                await socket.SendAsync(message);
            });
            
            server.Start(80);
            server.BlockWhileRunning();

The code reads all incoming messages and sends same message to sender.
Try to connect to **ws://localhost** in websocket.org.
After connected, send a message. Same message will be received.

After that, visit **http://localhost** on your favourite browser.
You should see 404 page of twino.
Because an HTTP server must be started on this port to accept websocket connections.

Then, you can implement your customized HTTP server on same port.
Suppose, we do not want to response 404, we want to response 400 bad request.
Here is new code:

            TwinoServer server = new TwinoServer();

            server.UseHttp(async (request, response) =>
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await Task.CompletedTask;
            });
            
            server.UseWebSockets(async (socket, message) =>
            {
                Console.WriteLine($"# {message}");
                await socket.SendAsync(message);
            });
            
            server.Start(80);
            server.BlockWhileRunning();
            
