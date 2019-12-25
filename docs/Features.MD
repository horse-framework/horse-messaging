All Twino Features are listed below.


## Twino Server

* Lightweight and high speed TCP Server
* Fully multi-threaded server
* Supports all protocols with an interface implementation
* Supports multiple port and host bindings
* Supports SSL certificates
* Supports Content Encodings over all protocols: gzip, deflate and brotli
* Has ping, pong and keep alive managers for all implemented protocols

## Twino TMQ

* TMQ is a messaging protocol, you can read more about TMQ Protocol [here](https://github.com/mhelvacikoylu/twino/blob/v2/docs/mq/TMQ%20Protocol.MD)
* TMQ Protocol message readers and writers
* TMQ Protocol Handlers
* Unique Id generators
* Has server side socket classes for using on TMQ protocol servers

## Twino MQ

* Twino MQ is a complete messaging queue server library and can be used with a few implementation
* Channel and content type based comminucation in queues
* Supports unlimited channels
* Each channel supports 65k queues
* Loading channels from JSON file
* Loading channels programmatically
* Adding and removing channels at runtine
* Adding and removing queues at runtime
* Changing queue statuses and workflows at runtime
* Adding, removing and updating channel and queue remotely
* Client based authentication
* Channel based authentication
* Client based authorization; create channel and queue, send message to queue, send message to other client, pull from queue
* Extensible channel event management; queue created and removed, client joined and left, status changed
* Client name, token, type and other custom headers are supported
* Multiple instances are supported can work distributed
* For distributed instances authentication and message routing options
* TmqClient client class for connecting MQ servers
* Quick join, leave, push, send methods

#### For Each Queue Features

* Sending to only first acquirer option
* Requesting acknowledge and sending automatically or manually options
* Acknowledge timeout option
* Messages, pending in queues, timeout option
* Using or not using unique message id
* Waiting acknowledge for previous message before send next message option
* Hiding or showing producer client unique id option
* Three type queuing with statuses route, push, pull
* Pausing and resuming queue options
* Stopping and restarting queues

#### For Each Message Features

* High priority messages
* Tracking message receivers
* Tracking message acknowledged
* Sending response message to a message
* Saving messages
* Message TTL option
* Advanced extensible messaging worklow with interface implementation
  * Decide what should to do when message is received
  * Decide what should to do when message is started to send to queue receivers
  * Decide for each single consumer if it can receive the message or not
  * After sending a message to a single consumer operations
  * After completed sending message operations to all consumers
  * Send completion decisions
  * Decide if message should be kept or removed
  * After message removed operations
  * Message acknowledge received operations
  * Message acknowledge timed out operations
  * Message queue timed out operations
  * Message delivery exception handling

## Twino HTTP

* Lightweight HTTP Request and response with quick action method
* Advanced HTTP Request handling with interface implementations
* Request querystrings
* Request forms
* Multipart form data and file uploads
* HTTP Reader and writer classes for developers
* Content writers are supported for content encodings
* Fully multi-threading HTTP Request handling

## Twino WebSockets

* Over Twino HTTP or only websocket usage
* Quick usage with message receive action handler
* Advanced usage with advanced interface implementations
* Client management and client containers
* WebSocket reader and writer protocol classes for developer usage
* HTTP Request headers over websockets
* Advanced WebSocket clients for connecting to websocket servers

## Twino Serializable Models

* Sending and receiving objects over websocket protocol
* Auto serialize and deserializing with model writers and readers
* Getting objects directly from the network with package reader

## Twino MVC

* Nearly same usage with ASP.NET Core and 2x faster than ASP.NET Core
* Controllers derive from TwinoController
* TwinoController has UserPrincipal, HttpRequest, HttpResponse, Server properties
* MVC uses Twino IOC, each request work in a scope, you can reach scope, if you need, inside controller
* Controller constructors fully support Twino Ioc dependency injection
* Middlewares with IMiddleware interface implementation
* Milddeware constructors fully support Twino Ioc dependency injection
* TwinoMvc has init and use methods, in use method middlewares can be used ASP.NET like
* Custom controller factory implementation is supported with IControllerFactory
* Route, HttpGet, HttpPost... attributes can be applied to controller actions
* Route attribute can be applied to controllers
* Route attribute supports patterns like {name}, {?name}, [controller] and [action] 
* Action filters and controller filters are supported (IActionFilter and IControllerFilter interface implementations)
* Custom error handling
* Custom default status code result contents
* FileResult, JsonResult, XmlResult, StringResult, HtmlResult, StatusCodeResult objects for quick responses, also controllers have methods to create quick results
* Assembly injection, loading controllers from other assemblies
* Using static files
* Multiple static file folder on same path and vice versa
* Static file using authentication methods
* Useful, easy to use and extensible policy management
* Authorize attribute for authorization

## Twino Connectors

* Extensible connector base classes
* Multiple host support
* Advanced event management
* Extensible and overridable
* Single message connector for sending single messages to servers, connects automatically when message is sending and disconnects after message is sent
* Necessity connector, connects only when a new message is about to send and keeps the connection after operation
* Sticky connectors, keeps always connected to specified servers
* Absolute connectors for not to lose any message, even client is not connected

## Twino IOC

## Twino Data Extensions

## Twino HTTP Extensions
