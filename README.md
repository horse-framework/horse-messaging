# What's Twino?

**Twino** is a .NET Core Framework that connects applications. Twino connection goal is providing solution for all communication methods. Twino offers the following libarries for this purpose:

* Twino Messaging Queue Server allows queuing such as Push, Pull, Cache, Broadcast and more
* Each client that connected to Twino Server can send messages, requests and responses each other.
* Clients can subscribe events and gets information when another client is connected or does something.
* Twino supports HTTP Server and MVC Architecture similar to ASP.NET MVC
* Twino supports WebSocket Server.
* Twino has an IOC Library with service pools and scopes


See **[All Twino NuGet Packages](https://www.nuget.org/packages?q=twino)**

**Go to [Documentation Home Page](https://github.com/mhelvacikoylu/twino/tree/v3/docs)**

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
