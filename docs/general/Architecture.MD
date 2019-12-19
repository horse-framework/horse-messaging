## Twino Library Architecture and Layers

All twino libraries are built on the Twino Server foundation.
Twino Server is a simple TCP server that handles the connections, tracks their timeout operations.

Twino server supports multiple protocols and you can create your own protocol and implement it to twino server.
When a connection request is accepted, Twino Server reads first 8 bytes from the network stream.
These 8 bytes are protocol decision bytes.
Twino checks all implemented protocols with these bytes.
If an implemented protocol accepts the received data, Twino Server stwitches the connection's protocol.
After the protocol handshake operation, if protocol tells to Twino Server that is piped/long running protocol Twino Server keeps the connection.
Piped connections should have a ping/pong mechanism, Twino Server can ping the client periodically but protocol must provide the ping and pong data to the server.

In Twino, by default, there are three protocols **TMQ**, **HTTP** and **WebSockets**.
Here is the layer architecture of twino below.

![architecture](https://github.com/mhelvacikoylu/twino/blob/v2/docs/diagrams/architecture.png)

* Twino Mvc is a HTTP Server architecture runs on Twino HTTP protocol.
* Twino IOC is a dependency injection library used by some Twino libraries.
* Twino has several IOC extensions such as Data (for Entity Framework Db connections) and Http (for http client pooling)
* Serializable models are for sending and receiving models over websockets.
* Twino has a WebSocket client uses Twino Websocket Protocol
* Twino TMQ protocol library is independent TMQ protocol library
* Twino MQ is a messaging queue server that uses TMQ protocol library. You can create your own MQ server too with using TMQ Protocol Library.
* Connectors library provides some useful features for TMQ and WebSocket clients
