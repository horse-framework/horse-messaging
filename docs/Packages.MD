
# NuGet Packages

### Twino.Core
Twino.Core is the core project of the twino.
You don't need to reference this project directly on your projects.
All references you need already includes Twino.Core.

[Twino.Core](https://www.nuget.org/packages/Twino.Core)


### Twino.Server
Twino Server is a TCP server that can support many protocols.
This project is just a TCP server and if you do not implement a custom protocol, you don't need to reference this project.

[Twino.Server](https://www.nuget.org/packages/Twino.Server)<br>

### Twino.Client.Connectors
Connector project can be used for all protocol clients in Twino.
Connectors are wrapper classes for clients and have some features such as auto reconnect, keep alive, auto instance creation etc.

[Twino.Client.Connectors](https://www.nuget.org/packages/Twino.Client.Connectors)<br>

### HTTP Packages
HTTP Packages are for supporting HTTP protocol in Twino Server.
If you need to create a simple HTTP server you can directly use Twino.Protocols.Http package.
If you need to create more complex HTTP server or you just want to use MVC, you can use Twino.Mvc.
For using JWT Token authentication you can use Twino.Mvc.Auth.Jwt package too.

[Twino.Protocols.Http](https://www.nuget.org/packages/Twino.Protocols.Http)<br>
[Twino.Mvc](https://www.nuget.org/packages/Twino.Mvc)<br>
[Twino.Mvc.Auth.Jwt](https://www.nuget.org/packages/Twino.Mvc.Auth.Jwt)<br>

### WebSocket Packages
WebSocket Packages are for supporting WebSocket protocol.
To create new WebSocket server you can use Twino.Protocols.WebSocket package.
If you need to connect to a WebSocket server, you can use Twino.Client.WebSocket package.
If you need a quick way to model serialization via WebSocket Twino.SerializableModel package can be helpful.

[Twino.Protocols.WebSocket](https://www.nuget.org/packages/Twino.Protocols.WebSocket)<br>
[Twino.Client.WebSocket Client](https://www.nuget.org/packages/Twino.Client.WebSocket)<br>
[Twino.SerializableModel](https://www.nuget.org/packages/Twino.SerializableModel)<br>

### Twino Messaging Queue Packages
Message Queue packages are using TMQ Protocol which is Twino's own Messaging Queue protocol.
For more information read [TMQ Protocol](https://github.com/mhelvacikoylu/twino/blob/v2/docs/TMQ%20Protocol.MD) protocol file.
Twino has a MQ server in Twino.MQ Package.
If you want to create your own custom MQ Server you can use Twino.Protocols.TMQ package directly.
If you need to connect to a TMQ Server you can use Twino.Client.TMQ Package.

[Twino.Protocols.TMQ](https://www.nuget.org/packages/Twino.Protocols.TMQ)<br>
[Twino.Client.TMQ Client](https://www.nuget.org/packages/Twino.Client.TMQ)<br>
[Twino.MQ Messaging Queue Server](https://www.nuget.org/packages/Twino.MQ)<br>

### IOC Packages
IOC Packages for IOC Containers are used mostly in Twino.Mvc projects.
Twino.Extensions.Data has implementations for EntityFramework Core and DbContext implementations.
Twino.Extensions.Http has implementations for HttpClient and factories.

[Twino.Ioc](https://www.nuget.org/packages/Twino.Ioc)<br>
[Twino.Extensions.Data](https://www.nuget.org/packages/Twino.Extensions.Data)<br>
[Twino.Extensions.Http](https://www.nuget.org/packages/Twino.Extensions.Http)<br>
