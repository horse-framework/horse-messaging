# Twino

**Twino** is a .NET Core Web Server.<br>
Twino provides advanced WebSocket server management, messaging queue infrastructure, HTTP Server, MVC pattern similar to ASP.NET Core and WebSocket clients.

## Why Twino?

- High performance (in many cases, as fast as kestrel)
- Twino is high scalable advanced websocket server with amazing client management.
- WebSocket and HTTP server on same project, same port, same host.
- Twino.Mvc has nearly all features ASP.NET MVC has, and you write nearly same code.
- Websocket Clients with sweet connectors and you can send and receive objects via websocket.

### Libraries

**Twino.Core:** Twino Core contains all common features for all Twino libraries. TCP connection handling, DNS resolving, HTTP Procotol implementation, WebSocket Protocol implementation etc.<br>
**Twino.Server:** Basic HTTP and WebSocket server. Accepts HTTP and WebSocket requests and process these requests.<br>
**Twino.Client:** Twino WebSocket Client. Also includes connectors for specific purposes: AbsoluteConnector, StickyConnector, SingleMessageConnector etc.<br>
**Twino.Mvc:** MVC Architecture for Twino.Server. It's quite similar to ASP.NET Core. Supports most useful attributes of ASP.NET Core, has its own authentication, authorization and policy management system. Has its own Service container, Middleware system and action filters.<br>
**Twino.Mvc.Auth.Jwt:** JSON Web Token implementation for Twino.Mvc library<br>
**Twino.SocketModels:** Makes WebSocket communication super easy and event/model based. With Twino.SocketModels, you do not need any knowledge about WebSocket protocol to use it.<br>

##### NuGet Packages

[Twino.Core NuGet Package](https://www.nuget.org/packages/Twino.Core)<br>
[Twino.Server NuGet Package](https://www.nuget.org/packages/Twino.Server)<br>
[Twino.Client NuGet Package](https://www.nuget.org/packages/Twino.Client)<br>
[Twino.Mvc NuGet Package](https://www.nuget.org/packages/Twino.Mvc)<br>
[Twino.Mvc.Auth.Jwt NuGet Package](https://www.nuget.org/packages/Twino.Mvc.Auth.Jwt)<br>
[Twino.SocketModels NuGet Package](https://www.nuget.org/packages/Twino.SocketModels)<br>
<br>

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


### Basic MVC Example

Twino.Mvc similar to ASP.NET Core. Here is a basic example:

    class Program
    {
        static void Main(string[] args)
        {
            IClientFactory factory = new ClientFactory();
            using (TwinoMvc mvc = new TwinoMvc(factory))
            {
                mvc.Init();
                mvc.Run();
            }
        }
    }
	
    public class ClientFactory : IClientFactory
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            return await Task.FromResult(new Client(server, request, client));
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
