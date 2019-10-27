# Twino

**Twino** is a .NET Core library.<br>
Twino provides advanced WebSocket server management, messaging queue infrastructure, HTTP server, MVC pattern similar to ASP.NET Core, WebSocket client.

## Why Twino?

Twino is high scalable advanced websocket server with amazing client management. You can use WebSocket and HTTP server on same project, same port, same host. Twino.Mvc has nearly all features ASP.NET MVC has, and you write nearly same code. It has websocket client with sweet connectors and you can send and receive objects via websocket if they are derived some interfaces.

### Features

Some of Twino features are listed below:

- Basic WebSocket Server
- WebSocket Server Client Management
- Basic HTTP Server based on simple Request and Response
- Advanced HTTP Server with MVC pattern
- JWT support with Authorize attribute and custom token implementation
- Middlewares infrastructure ASP.NET like (including CORS Middleware)
- SSL Support with pfx, crt or custom string
- Multiple Host bindings with multiple ports (you don't need reverse proxy)
- WebSocket Clients
- Connectors for WebSocket Clients for different purposes
- HTTP and WebSocket server on same port
- Request and Response Architecture (non-HTTP) via one active TCP connection
- Object based data transfer with IModelWriter and IModelReader interfaces
- GZIP and Brotli content encoding support
- Application form, JSON and XML support on Twino.Mvc
- Custom and fast serialization helper libarary for objects

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

## Twino.Client

Twino.Client is a WebSocket client. Basic usage:

	TwinoClient client = new TwinoClient();
	client.MessageReceived += (sender, message) => { Console.WriteLine(message); };
	client.Connected += sender => Console.WriteLine("Connected");
	client.Disconnected += sender => Console.WriteLine("Disconnected");
	client.Connect("ws://localhost:8080");

Twino.Client has 4 connectors. Each connector has different purpose.
All connectors have same usage and they derive from ConncectorBase class and implement IConnector interface.

Usage for all connectors:

	//reconnects in 1000 milliseconds, if the connection closes. This is StickyConnector's feature.
	ConnectorBase connector = new StickyConnector(TimeSpan.FromMilliseconds(1000));
	
	//at least one host must be added to connect.
	connector.AddHost("ws://127.0.0.1");
	connector.AddHost("wss://alternative-if-first-fails.host");
	
	//extra headers
	connector.AddHeader("Authorization", "Bearer xyz123...");
	
	connector.Connected += sender => Console.WriteLine("Connected");
	connector.Disconnected += sender => Console.WriteLine("Disconnected");
	connector.MessageReceived += (sender, message) => { Console.WriteLine(message); };
	connector.ExceptionThrown += (sender, exception) => Console.WriteLine(exception);
	
	//runs the connector, connects to the server.
	connector.Run();
	
	//sends a message
	connector.Send("Hello world!");
	
	//disconnects from the server and aborts reconnection
	connector.Abort();
	
	//gets the current client.
	//if you need to use this client, do not keep it.
	//if connector reconnects to the server, you should re-get client with GetClient method
	//old client will be disposed.
	TwinoClient currentClient = connector.GetClient();
	
	//how much time past after Run is called
	Console.WriteLine(connector.Lifetime);
	
	// how many times connected to the server (includes reconnections)
	Console.WriteLine(connector.ConnectionCount);
	
	//is connected, true
	Console.WriteLine(connector.IsConnected);
	
	//is running, true. different from IsConnected.
	//connections may fail but it might still run and tries for reconnections.
	Console.WriteLine(connector.IsRunning);

NOTE: You can use all connectors same code (above), you neeed to change only class name; StickyConnector, AbsoluteConnector, NecessityConnector and SingleMessageConnector.

### Sticky Connector

It's most used connector. Sticky Connector connects to the server. If it's disconnected, reconnects again. It always keeps the connection alive. You can set, how much time wait before next connection. If you don't waste any time between connections you can set it TimeSpan.Zero, but when the remote server is down, it may consume more resources. Minimum 200ms delay is recommendeded.

### Absolute Connector

Derives from Sticky connector. The only different from sticky connector, absolute connector has it's own send buffer. If client tries to send a message when the connection is closed. It keeps the data and sends to the server when connection established (ASAP). If you don't want to lose any data you are sending, Absolute Connector is a better choice. But it consumes more memory and cpu than sticky connector. On the other hand, if the time between connections takes longer, buffered data may be misleading.

### Necessity Connector

This connector does not reconnect to the server after disconnected. But when Send method is called for sending a message, if the connection closed, it connects and sends the message. After this operation it keeps the connection alive.

### Single Message Connector

This connector derives from Necessity Connector. The only difference is, it closes the connection after a message is sent. But reconnects again when the Send method is called. Works like HTTP requests. Each send is creates new connection and closed after sending operation is completed.

## Twino.Server

Twino is created for creating HTTP Server, WebSocket server or both of them.
on HTTP procotol, you have to handle the HTTP request. Even if you want to create only WebSocket server. For more information you can read [RFC 6455 - The WebSocket Protocol](https://tools.ietf.org/html/rfc6455). But you can ignore HTTP Request handling in Twino. Let's look all usages for Twino.Server.

I prefer to use always Twino.Mvc. Twino.Server is underlying library of Twino.Mvc, using Twino.Server directly may be harder/confused. Twino.Mvc looks like ASP.NET Core. With small changes, you can copy/paste ASP.NET Core codes and go on with Twino.Mvc. If you want to use HTTP Server for a small project (or a part of project) with a few endpoints or create only WebSocket server, I can recommend Twino.Server. But in more complex projects, Twino.Mvc will be much better.

### Only HTTP Server

All HTTP Requests will be handled in IHttpRequestHandler interface's Request method. In order to handle HTTP Requests, you have to implement this interface and pass it into TwinoServer as parameter.

    public class RequestHandler : IHttpRequestHandler
    {
        public async Task RequestAsync(TwinoServer server, HttpRequest request, HttpResponse response)
        {
            response.SetToHtml();
            response.Write("<html><head></head><body>Hello World!</body></html>");
            await Task.CompletedTask;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            RequestHandler handler = new RequestHandler();
            TwinoServer server = TwinoServer.CreateHttp(handler);
            server.Start(80);
        }
    }

### Only WebSocket Server

After SSL and WebSocket handshaking is completed and the request is validated as WebSocket request, Twino.Server calls IClientFactory interface's Create method to create new socket class for the specific connection.

If you want to customize your connections, you can derive your class from ServerSocket class. Or you can use the default server socket class and work with it's events.

    public class CustomClient : ServerSocket
    {
        public CustomClient(TwinoServer server, HttpRequest request, TcpClient client) :
		base(server, request, client)
        {
        }

        protected override void OnConnected()
        {
            Console.WriteLine("Client Connected");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine("Client Disconnected");
        }

        protected override void OnMessageReceived(string message)
        {
            Console.WriteLine(message);
        }
    }
    
    public class ClientFactory : IClientFactory
    {
        public async Task<ServerSocket> Create(TwinoServer server, HttpRequest request, TcpClient client)
        {
            return await Task.FromResult(new CustomClient(server, request, client));
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            IClientFactory factory = new ClientFactory();
            TwinoServer server = TwinoServer.CreateWebSocket(factory);
            server.Start(85);
        }
    }

### HTTP Server with WebSocket Server

If you want to use both of HTTP and WebSocket servers, you need to pass both IClientFactory and IHttpRequestHandler interfaces to Twino Server.

	TwinoServer server = new TwinoServer(handler, factory);

### Options

TwinoServer needs some options to run. You can set the options with ServerOptions class or with options json file.
If you choose JSON file implementation, TwinoServer requires one of these files **server.json**, **twino.json**

Sample TwinoServer JSON Options file seems like;

    {
       "PingInterval": 60000,
       "HttpConnectionTimeMax": 120,
       "MaximumRequestLength": 2097152,
       "MaximumUriLength": 1024,
       "MaximumHeaderLength": 4096,
       "MaximumPendingConnections": 100,
       "RequestTimeout": 15000,
       "ContentEncoding": "gzip",
       "Hosts": [
           {
             "Port": 80,
             "SslEnabled": false,
             "SslCertificate": null,
             "CertificateKey": null,
             "SslProtocol": "Tls12",
             "Hostnames": ["www.github.com", "www.example.net"]
           },
           {
             "Port": 81,
             "SslEnabled": false,
             "BypassSslValidation": true
           }
       ]
    }

* **PingInterval** is ping interval from server side to client side in milliseconds for WebSocket clients.<br>
* **HttpConnectionTimeMax** maximum duration in milliseconds for keeping alive http connections.<br>
* **MaximumRequestLength** is after TCP connection is accepted, maximum data length of first request in bytes.<br>
* **MaximumUriLength** is maximum URL length of the HTTP Request (path in first line).<br>
* **MaximumHeaderLength** is maximum header length until the request content (until the blank line).<br>
* **MaximumPendingConnections** is maximum pending connection in queue size. When the limit is exceeded next connection request, the request is rejected instantly.<br>
* **RequestTimeout** in milliseconds. If the handshake could not complete in this time, connection will be closed. In this case handshaking consists of SSL handshaking, HTTP Request reading and WebSocket handshaking.<br>
* **ContentEncoding** is response content encoding type. For no encoding, If you dont use content encoding, set value **null**. Supported encodings are **gzip** and **br** (brotli). If you create options with **CreateDefault** method, content encoding will be **gzip**.
* **Hosts** is for bindings. Twino server can listen multiple ports with different SSL options. If you want to listen a port for only specific domain names you can set these domain names to **Hostnames** (string array) property. In order to accept all requests from all hostnames you can set this value *. For each host options, If SslEnabled is true, connections will be SSL protected. If you pass SslCertificate null, streaming will be NegotiateStream. If you set SslCertificate ".crt" or ".pfx" file streaming will be SslStream. If you have passphrase on your certificate file you can set **SslCertificateKey** value the password. If you don't have password, set Key value empty (null might not work propertly on some systems). If you want to ignore SSL erros set **BypassValidation** value **true**<br>

You can change your Twino settings programmatically when needed.

	server.Options.PingInterval = 30000;
	server.Options.MaximumPendingConnections = 100;

## Twino.Mvc

Twino.Mvc similar to ASP.NET Core. Here is a basic example:

    class Program
    {
        static void Main(string[] args)
        {
            using (TwinoMvc mvc = new TwinoMvc())
            {
                mvc.Init();
                mvc.Run();
            }
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

### Creating WebSocket Server with Twino.Mvc

You can handle WebSocket requests with Twino.Mvc. You just need to pass IClientFactory interface to TwinoMvc.
Here is a basic example:

    IClientFactory factory = new ClientFactory();
    using (TwinoMvc mvc = new TwinoMvc(factory))
    {
    	mvc.Init();
    	mvc.Run();
    }

### Services

Twino Service container supports transient and singleton services. ASP.NET Core like scope services are not supported.
Twino Services can be passed as constructor parameter to controller types derived from TwinoController.

    class Program
    {
        static void Main(string[] args)
        {
            using (TwinoMvc mvc = new TwinoMvc())
            {
                mvc.Init(twino =>
                         {
                             //adds transient service.
                             twino.Services.Add<IDemoService, DemoService>();

                             //adds singleton service with generic types. it's created on first usage.
                             twino.Services.AddSingleton<IDemoService, DemoService>();
                             
                             //adds singleton service via non-generic method. it's created on first usage.
                             twino.Services.AddSingleton(typeof(IDemoService), typeof(DemoService));
                             
                             DemoService pre = new DemoService();

                             //adds singleton service. it's created on init, not first usage
                             twino.Services.AddSingleton<IDemoService, DemoService>(pre);
                             
                             //non-generic version
                             twino.Services.AddSingleton(typeof(IDemoService), pre);
                         });

                mvc.Run();
            }
        }
    }

In most common cases you need to use your registered services from controllers. So you can inject your services to controllers' constructors. But sometimes you might need to use these services from another class, thread or assembly. Here is a basic example shows how can you do that:

    class Program
    {
        public static TwinoMvc Server { get; private set; }

        static void Main(string[] args)
        {
            Server = new TwinoMvc();
            Server.Init(r => { r.Services.Add<IDemoService, DemoService>(); });
            Server.Run();
        }
    }

    public class AnotherClass
    {
        public void AnotherMethod()
        {
            IDemoService service = Program.Server.Services.Get<IDemoService>();
            
            //do something with service...
        }
    }

### Using Middlewares

Twino Middlewares similar to ASP.NET Core middlewares as usage.
Twino middlewares must implement IMiddleware interface. Service collection injection does not work in middlewares. If you want to use service from collection, you have to call Get method of IServiceCollection.

In order to break the middleware chain, next method must be called with non null parameter. If the parameter is not null, Twino will return the parameter as response and does not move next middlewares.

Here is a useful middleware example:

    public class CustomMiddleware : IMiddleware
    {
        public void Invoke(NextMiddlewareHandler next, HttpRequest request, HttpResponse response)
        {
            //injections are not supported on middlewares.
            //you can call the service like this.
            IDemoService service = Program.Server.Services.Get<IDemoService>();
            
            //call with null parameter to move next middleware
            next(null);
        }
    }
    
    class Program
    {
        public static TwinoMvc Server { get; private set; }

        static void Main(string[] args)
        {

            using (TwinoMvc mvc = new TwinoMvc())
            {
                Server = mvc;

                mvc.Init();
                mvc.Run(app =>
                        {
                            //crate new middleware instance
                            //middlewares works with objects, not with types.
                            //so you can pass singleton middleware for each request if you want.
                            CustomMiddleware middleware = new CustomMiddleware();

                            app.UseMiddleware(middleware);
                        });
            }
        }
    }
    

### Error Handling

In Twino, you can specify custom Not Found and Error pages. Error Handling in Twino works with IErrorHandler interface. In this interface you can response custom error result and log the exception.

If you set true **IsDevelopment** mode, Twino's development error handler overwrites your error handler.

Here is a basic example:

    public class CustomErrorHandler : IErrorHandler
    {
        public void Error(HttpRequest request, Exception ex)
        {
            request.Response.SetToJson(new {Message = "Internal Server Error "});
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            using (TwinoMvc mvc = new TwinoMvc())
            {
                mvc.Init(twino =>
                {
                    twino.IsDevelopment = false;
                    twino.NotFoundResult = new JsonResult(new {Message = "Page not found"});
                    twino.ErrorHandler = new CustomErrorHandler();
                });
                mvc.Run();
            }
        }
    }

## Twino.Mvc.Auth.Jwt

Twino MVC supports authorization with Authorize attribute. You can also create policies for authorization.<br>
Here is simple code for Authentication and Authorization using Twino.Mvc.Auth.Jwt library and some basic policies.<br>

**Initialization**

    using (TwinoMvc mvc = new TwinoMvc())
    {
        mvc.Init(twino =>
            {
                twino.AddJwt(options =>
                {
                    options.Key = "Very_very_secret_key";
                    options.Lifetime = TimeSpan.FromHours(1);
                    options.ValidateLifetime = true;
                });
                twino.Policies.Add(Policy.RequireRole("PolicyName", "Admin"));
            });
        mvc.Run();
    }
    
**Sample Controller**

    [Route("api/[controller]")]
    public class SimleController : TwinoController
    {
        [HttpGet("go/{?id}")]
        [Authorize(Roles = "Role1,Role2")]
        public async Task<IActionResult> Go(int? id)
        {
            return await StringAsync("Go !");
        }

        [HttpPost("[action]")]
        [Authorize("PolicyName")]
        public async Task<IActionResult> Post([FromBody] SampleModel model)
        {
            return await JsonAsync(model);
        }
    }
    
    
## Twino.SocketModels

SocketModels is created for making WebSocket communication easier and type safer.
In order to use SocketModels, you need to implement your objects, which will be sent and received via socket, ISocketModel. ISocketModel is an interface has a property named Type with Int32 type. Each object must has it's unique Type number.

Here is some example models:

    public class LoginModel : ISocketModel
    {
        public int Type { get; set; } = KnownModelTypes.Login;

        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResultModel : ISocketModel
    {
        public int Type { get; set; } = 200;

        public bool Success { get; set; }
    }

After model implementations, now you can send and receive these objects. You can subscribe package manager's events and gets the messages when they arrived.

Here is a basic PackageReader usage:

    PackageReader pm = new PackageReader();

    pm.On<LoginModel>((socket, message) =>
    {
        Console.WriteLine($"login received {message.Username} {message.Password}");
        LoginResultModel result = new LoginResultModel();
        result.Success = true;
        socket.Send(result);
    });

And you need one more step. When a new message is received, PackageManager must read the message and call subscribers' events. So, you need to set PackageReader to read messages.

Here is a basic usage:

    twino.UsePackageReader(pm);
    
### How It Works

Socket models uses JSON serialization, sends and receives the messages as JSON serialized (string). TwinoModelWriter and TwinoModelReader classes do this. They can also create byte array data (WebSocket protocol data).

Model creating example:

	LoginModel model = new LoginModel();
	TwinoModelWriter writer = new TwinoModelWriter();
	string serialized = writer.Serialize(model);
	byte[] prepared = WebSocketWriter.CreateUTF8(model);
	
Using prepared data is better in some cases. When you have a string data to send via WebSocket, Twino.Core will create new byte array from this value. Because WebSocket protocol needs some meta data in binary format. If you want to send same data to 5000 clients, with Send(string) method, this operation will be done 5000 times. If you send the data with String(byte[]) this process will be done 1 time (you will do this manually with prepare method and pass the byte array as parameter)

Twino socket models converts your ISocketModels to array. Array's first element is the type of the model, second element is the model. This increases performance in some cases (actually in many cases).

Serialized data format looks like:

	[type, model]

Example:

	[101, { type: 101, username: "user@name", password: "s0!strong" }]
	
When you are working with socket models, if your client is javascript WebSocket (usually it will) you will need to serialize and parse this data in javascript manually.

In Twino, TwinoModelReader class does this automatically:

	string serialized = "[101, { type: 101, username: 'user@name', password: 's0!strong' }]";

	TwinoModelReader reader = new TwinoModelReader();
	//only deserializes the first element of array and returns (101)
	int modelType = reader.ReadType(serialized);

	//deserializes data. 3rd parameter is verify.
	//checks if first element or array equals type property of the model
	ISocketModel model = reader.Read(typeof(LoginModel), serialized, true);
	
	//better and easier usage.
	LoginModel genericModel = reader.Read<LoginModel>(serialized);



### Customize Socket Models

In many cases, using easy methods and moving on shortcuts is not the best way.
SocketModels support good customization and multiple packaging on Twino.

There is an example below with Socket Models. This is a client example. But can be used on server-side without any changes. In this example, server is using multiple package managers and in some cases, models are serialized manually for some reasons.

	PackageReader pack1 = new PackageReader();
	PackageReader pack2 = new PackageReader();
	
	pack1.On<LoginResultModel>((client, model) => { Console.WriteLine($"# Login Result: {model.Success}"); });
	pack2.On<ChannelJoinModel>((client, model) => { Console.WriteLine("another category"); });
	
	TwinoClient clientSocket = new TwinoClient();
	
	//this was easiest way, using a package manager with extension method.
	//clientSocket.UsePackageReader(pm);
	
	clientSocket.MessageReceived += (sender, msg) => {
		pack1.Read(sender, msg);
		pack2.Read(sender, msg);
	};
	
	clientSocket.Connect("127.0.0.1", 85, false);
	
	TwinoModelWriter writer = new TwinoModelWriter();
	string serialized = writer.Serialize(new LoginModel
	{
		Username = "test",
		Password = "123"
	});
	byte[] data = WebSocketWriter.CreateUTF8(serialized);
	
	clientSocket.Send(data);

You can also create your model writer and model reader classes with implementing IModelReader and IModelWriter interfaces. When you pass your IModelReader instance to PackageReader as constructor parameter, package reader uses your custom model reader.

### Fast Serialization with IPerformanceCriticalModel

Twino uses Newtonsoft JSON library as default serialization operations. ISocketModel models are serialized with Newtonsoft JSON. But using auto serialization sometimes may hurt performance. Almost all JSON serialization libraries have same issue.

As a solution, twino provides IPerformanceCriticalModel for manual serialization and deserialization. IPerformanceCriticalModel derives from ISocketModel, so you do not need to implement ISocketModel interface if you implemented IPerformanceCriticalModel.

Here is a sample implementation of IPerformanceCriticalModel

    public class CriticalModel : IPerformanceCriticalModel
    {
        public int Type { get; set; } = 302;

        public string Name { get; set; }
        public int Number { get; set; }

        public void Serialize(LightJsonWriter writer)
        {
            writer.Write("type", Type);
            writer.Write("name", Name);
            writer.Write("number", Number);
        }

        public void Deserialize(LightJsonReader reader)
        {
            Type = reader.ReadInt32();
            Name = reader.ReadString("name");
            Number = reader.ReadInt32();
        }
    }

**IMPORTANT NOTE:** On serialization, you need to specify property names, because the model should be readable from everywhere (different libraries, programming languages, platforms). But on deserialization operation, property order SHOULD be same with serialization. With this obligation, property names are not read and they are not searched in type's property list. This increases performance, what we need while using IPerformanceCriticalModel. If you want to verify that you are reading correct property of model, you can pass property name as arguement. This will activate property name check. However property serialization order SHOULD be same. If property is in different index, JSON deserializer will NOT find the property in JSON string, an exception will be thrown.


### NON-HTTP Requests with WebSockets

NON-HTTP Requests are sent and received on a websocket connection. Concept is being only one TCP connection but there are many requests simultaneously. Each request has it's own unique id and responses are caught by these unique ids. Benefits of using NON-HTTP requests according to use core websocket TCP communication, you can read the response with await or Task right after request is sent

Here an example, client sends a request to a server on websocket connection:

            RequestManager requestManager = new RequestManager();
            TwinoClient client = new TwinoClient();
            client.Connect("ws://127.0.0.1");
            RequestModel model = new RequestModel();
            SocketResponse<ResponseModel> response = requestManager.Request<ResponseModel>(client, model).Result;
	    

Here an example, server handles requests and responses them:

            RequestManager requestManager = new RequestManager();
            requestManager.On<RequestModel, ResponseModel>(request =>
            {
                ResponseModel response = new ResponseModel();
                return response;
            });

            ServerOptions options = ServerOptions.CreateDefault();
            TwinoServer server = TwinoServer.CreateWebSocket(async (server, request, client) =>
            {
                ServerSocket socket = new ServerSocket(server, request, client);
                socket.MessageReceived += (s, m) => requestManager.HandleRequests(s, m);
                return await Task.FromResult(socket);
            }, options);

            server.Start(80);

## Samples

There are 7 sample projects in the solution.

* **Sample.WebSocket.Client** is an example for Twino.Client project. It doesn't include Connectors.
* **Sample.WebSocket.Server** is an example for only WebSocket Server project. It doesn't include HTTP server and does not use Twino.Mvc.
* **Sample.Connectors** is an example for Twino.Client and Connectors.
* **Sample.Http.Server** is an example for only HTTP Server project. It doesn't include WebSockets and does not use Twino.Mvc.
* **Sample.Full.Server** is an example for full (HTTP and WebSocket) project (non-MVC)
* **Sample.Mvc** is an example for Mvc HTTP Server project without WebSocket integration.
* **Sample.SocketModels** is an example for SocketModels with WebSocket Server and Client.

