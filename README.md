# Twino

**Twino** is a .NET Core TCP Server provides multiple protocols on same host.<br>
**Twino** is a complete Messaging Queue server library.<br>
**Twino** is a WebSocket server with advanced client management.<br>
**Twino** is a HTTP server with MVC Support.<br>

Twino HTTP Server supports MVC architecture.<br>
Twino WebSockets provides advanced WebSocket server management<br>
Twino MQ provides quick messaging queue server library<br>
Twino IOC can be used on all protocols.

## Why Twino?

In a single application with single library, you can have TCP Server, HTTP Server, WebSocket Server, Messaging Queue Server and with many extra features such as IOC, Authentication, Clients, Connectors etc.

- High performance (in many cases, as fast as kestrel)
- Twino has high scalable advanced websocket server with amazing client management.
- Multiple protocols can be used on same project, same port, same host.
- Twino.Mvc has nearly all features ASP.NET MVC has, and you write nearly same code.
- Twino has sweet client connectors for WebSocket and TMQ protocols.
- Twino MQ is not a executable messaging queue server. It's library and you can create your own MQ server with a few interface implementations.

### Features

Some of Twino features are listed below:

#### HTTP Server

- HTTP and HTTPS support
- Loading SSL certificate programmatically, from file or from string
- Connection keep-alive or close options
- Maximum URI, Header, Content length options
- Content encoding suports (brotli, gzip and deflate)
- Multiple host and multiple port binding
- URL Encoded application form requests are supported
- Multipart form data requests are supported
- File upload and file download
- Full async support

#### MVC

- Middlewares infrastructure ASP.NET like (including CORS Middleware)
- JWT support with Authorize attribute and custom token implementation
- Using static files with multiple volume binding with validation actions
- Model binding from HTTP Request (JSON, XML, QueryString, FormData)
- Service collection for dependency inversion (singleton and transient)
- Easy to use policy and claim management
- Action and controller filters
- Custom HTTP Status code pages

#### WebSockets

- All features included from HTTP and/or MVC
- HTTP and WebSocket server on same port
- Connectors for WebSocket Clients for different purposes
- Request and Response Architecture (non-HTTP) via one active TCP connection
- Object based data transfer with IModelWriter and IModelReader interfaces
- Custom and fast serialization helper libarary for objects

#### MQ

- Complete Messaging Queue library can be used with a few interface implementations and fully extensible
- Queuing or not queuing options
- Multiple queues in same channel
- Client based authentication
- Channel based authentication
- Action based authorization
- Message saving operations
- Peer to peer messaging
- Sending messages to some type of clients or some client groups
- Message acknowledge in queues, peer to peer messaging and server side messaging
- Keeping messages if there is no receiver and setting timeout
- Sending messages by pulling
- Creating channels and queues programmatically
- Content types by models
- Requesting and responsing messages
- Fully extensible client management
- Channel event handlers
- Changing decisions from all steps in message delivery operations
- Pausing and resuming channel or queue operations
- Instanced servers supported

### Basic WebSocket Server Example

Basic WebSocket Server creation example

    class Program
    {
        static void Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            server.UseWebSockets((socket, message) => { Console.WriteLine($"Received: {message}"); });
	    
	    //or advanced with IProtocolConnectionHandler<WebSocketMessage> implementation
            //server.UseWebSockets(new ServerWsHandler());
            server.Start(80);
            
            //optional
            _server.Server.BlockWhileRunning();
        }
    }

### Basic MQ Server Example

Basic MQ Server creation example

    class Program
    {
        static void Main(string[] args)
        {
            MqServerOptions options = new MqServerOptions();
            //set your options here
            
            MqServer mq = new MqServer(options);
            mq.SetDefaultDeliveryHandler(new YourCustomDeliveryHandler());
            TwinoServer twinoServer = new TwinoServer(ServerOptions.CreateDefault());
            twinoServer.UseMqServer(mq);
            twinoServer.Start();
            
            //optional
            _server.Server.BlockWhileRunning();
        }
    }


### Basic MVC Example

Twino.Mvc similar to ASP.NET Core. Here is a basic example:

    class Program
    {
        static void Main(string[] args)
        {
            TwinoMvc mvc = new TwinoMvc();
            mvc.Init();
            mvc.Use(app =>
            {
                app.UseMiddleware<CorsMiddleware>();
            });

            TwinoServer server = new TwinoServer();
            server.UseMvc(mvc, HttpOptions.CreateDefault());
            server.Start();
            
            //optional
            server.BlockWhileRunning();
        }
    }

    [Route("[controller]")]
    public class DemoController : TwinoController
    {
        [HttpGet("get/{?id}")]
        public async Task<IActionResult> Get([FromRoute] int? id)
        {
            return await StringAsync("Hello world: " + id);
        }

        [HttpPost("get2")]
        public async Task<IActionResult> Get2([FromBody] CustomModel model)
        {
            return await JsonAsync(new {Message = "Hello World Json"});
        }
    }
