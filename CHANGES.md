
## Renaming and Refactoring

- **ServerSocket** class namespace has changed to **Rim.Server.WebSockets** in **Rim.Server** assembly.
- ModelWriter and ModelReader classes are marked as obsolete. Instead of these static classes, you should use instances of **RimModelReader** and **RimModelWriter** classes. For quick singleton instances are defined as **RimModelReader.Default** and **RimModelWriter.Default**
- PackageManager's name is changed and it's obsolete now. Use **PackageReader** instead of this.
- SocketBase and RimServer extension UsePackageManager methods are obsolete now. Use **UsePackageReader** methods instead of these.
- For WebSocket protocol packaging, PackageReader and PackageWriter classes obsolete now. New names are **WebSocketWriter** and **WebSocketReader**. For preventing mis-understanding some method names are changed; **CreateUTF8** to **CreateFromUTF8**, **CreateBinary** to **CreateFromBinary**.
- Making serialization operations extensible, made difficult to use default serialization (at least old versions were easier) To solve this issue, a new extension method is added to **ISocketModel** interface: **ToWebSocketUTF8Data**, creates web socket protocol byte array data of the model.

**NOTE:** Obsolete usages will be removed in 3.2


## Multiple Host Bindings

  **NOTE:** Server options are changed! You need to update your rim.json or server.json file. If you set your options programmatically, new version will not be compiled.

* Now one server can have multiple bindings. Options' Host property is now an array

* Each binding has it's own port number, host names and SSL options

* If you start server with port parameter, only one binding will be used without SSL.
  This usage is created to start host simple and quickly.

* Each binding has it's own MaximumPendingConnections.
  Example, when MaximumPendingConnections is 10. If you bind 5 host, your server's total pending max connection will be 50 (10 for each)

* All bindings use same firewall options. If an IP address is blocked from one binding, others will reject same IP.

* Bindings work async. If there is a high traffic one of them, others will not be affected.

* Usage information is in README.md, you can also check sample projects.


## Fast Serialization with IPerformanceCriticalModel

* LightJsonReader and LightJsonWriter classes are added. Serializing and deserializing is more manually so it gives much more performance.

**NOTE:** Serialization property order is important. Serialization and deserialization SHOULD be same order. And you dont need to specify property name while reading and it gives more performance.

* a new interface is added: **IPerformanceCriticalModel**. It has serialize and deserialize methods using **LightJsonWriter** and **LightJsonReader**.

* If you do not create your own IModelReader and IModelWriter classes, RimModelReader and RimModelWriter classes are used. This classes check if the model implemented from IPerformanceCriticalModel. If it's, as serialization method Serialize ve Deserialize methods are used. If not, Newtonsoft.JsonConvert class is used.

* Usage information is in README.md, you can also check sample projects.


## NON-HTTP Request and Response with WebSockets

This is a new architecture, you may not have seen before.
In this architecture, via same TCP connection, multiple requests can be sent and responses can receive async.
Each request has it's own unique id and responses are sent with same unique id.
Requests and responses are find each other with this unique id.

That allows sending multiple users' multiple requests via same TCP connection async with no request and response header information.

**When you need to use this?**

For a basic example;

- You have an API endpoint like /do/something
- Each user posts some data to this endpoint
- Your endpoint does some work and calls another API behind, get's response and sends it to user.

**Disadvantages of using standart HTTP way:**

- For each request, you need to create new TCP connection to get real data from behind API.
- Each TCP connection probably will require another SSL handshake and more.
- Each TCP connection will generate Request headers.
- Each TCP connection will receive Response headers.
- Each TCP connection requires some authentication and authorization checks.
- Each TCP connection open and closing and other operations will cause some delay.
- If you need to scale your API, your behind API must be scaled with nearly same ratio.

**with Non-HTTP Requests**

- You connect to the server only one time. if you are using scaled infrastructure like Kubernetes, one connection for each instance.
- You can do authentication and authorizations one time. (if behind API's auth is not different for each user)
- You do not send request headers and do not get response headers. You just send and get model. (Rim has minimal header data about 50 bytes)
* Usage information is in README.md, you can also check sample projects.



## MVC Route features

Classes derive from RimController now support multiple Route attributes for multiple routing.

	//old
	[Route("[controller]")]
	public class HomeController : RimController

	//new
	[Route("[controller]")]
	[Route("home")]
	[Route("another-home")]
	public class HomeController : RimController


Action methods in RimController classes now support multiple HttpMethod bindings for multiple routing.

	//old
	[HttpGet("example")]
	public IActionResult Example()

	//new
	[HttpGet("example")]
	[HttpGet("other-get")]
	[HttpPost("other-post")]
	[HttpDelete("other-delete")]
	public IActionResult Example()

You can add custom assemblies to route builder now. If your referenced assemblies are loading indirectly, and controllers in these assemblies are not initialized automatically, you can load these controllers and their routes manually like code below.

	mvc.CreateRoutes(assembly);
