# Horse Messaging Documentation

[Back to Documentation Contents](README.md)

## Overview

This documentation is overview of server infrastructure and usage.
It contains superficial information about all features of Horse Messaging Server.

### Horse Server and Riders

Rider special word for Horse Server.
A rider, manages all operations for a specific feature.
An example Queue Rider manages all queue operations.
Route Rider for routers and bindings, Channel Rider for channels etc.

Horse Messaging Server's root object is HorseRider.
It manages all Horse Protocol operations and it contains properties of all riders.
You can use all riders like that way:

```cs
HorseRider rider = HorseRiderBuilder.Create()....Build();
...
rider.Queue.Create(...)
HorseChannel channel = rider.Channel.Find("channelName");
```

By the way, let's talk about server startup and other information.
HorseRider object manages all Horse Protocol operations.
But creating a TCP server and handling connections is done by HorseServer.
HorseServer is different nuget package with name Horse.Server.
HorseRider is an implementation for HorseServer.
So, we need to start a HorseServer and implement HorseRider to it.
Here is the sample code:

```cs
HorseServer server = new HorseServer();
server.UseRider(rider);
server.Start(26001);
```

The code above creates new TCP server on port 26001 and accepts connections
via Horse Protocol. UseRider is an extension method.
If you want, you can create your own protocol and implement it with UseProtocol method.
And it's possible use multiple protocols on same server.

Here is quick information for all features of Horse Messaging Server.

<br>

#### Queues

Queues are managed by QueueRider that can be found as property in HorseRider object.
Horse supports **Push**, **Pull** and **Round Robin** queues.
- **Push** queues send messages to all subscribers. Each message is sent to all consumers.
- **Pull** queues keep messages. Consumer sends a pull request and receives the message. Each message is sent to one consumer.
- **Round Robin** queues work like push queues but each message is sent to one consumers. Load balancing is done with round robin algorithm.

Horse supports memory and persistent queues by default. And there are good implementation options.
You can customize every feature of queues by implementing your custom interfaces.
You can find further information in [queues section](queues.md).

<br>

#### Channels

Channels are for streaming data to subscribers. Channels do not have a queuing system.
When producer procudes a message into a channels, the message is sent to all subscribers immediately.
Channels are best solution for broadcasting live data such as trading, live scores etc.
Channels are super fast and use low ram and cpu.
It's possible to send more than 400k messages per second over each channel.
There are some options, events and features for channels.
You can find further information in [channels section](channels.md)

<br>

#### Routers

Routers are for routing messages to bindings.
There are some predefined bindings in Horse and you can create your own binding too.
Bindings, binds messages to queues, channels, clients or http endpoints.
If you want to send your message to multiple targets, or do some specific operations before pushing into a queue,
you can use routers. There are 3 different routing options in routers and bindings.
- **Distribute** sends the message to all bindings.
- **Only First** sends the message to only first binding. Others are just wait like generator.
- **Round Robin** sends each message to only one binding with round robin algorithm.

<br>

#### Direct Messaging

Each client is just a connected client for Horse Server.
It does not matter what are connected for, consuming, producing etc.
Each client has a unique id. Client's can decide what their client id will be,
but if another client is already connected with same id, horse sets new unique id for the client.
That allows clients to send a message to a specific client.
Horse manages all cases, if client is not online, or cannot response etc.
In addition, each client can has Name and Type values (they are not unique).
When a client sends a message to a Name or Type, the messages is sent to all clients with same Name or Type.

<br>

#### Caching

Horse provides simple distributed caching system.
You can just set a cache key and value with expiration date,
and get when you want. You can also manage your keys, get all, remove some of them or purge all.

<br>

#### Events

Events are designed for clients. Especially for monitoring clients.
A client subscribes to an event. When the event occured in server,
servers sends the information about the event to the client.
There are more than 30 events in horse such as
Client connected, queue created, a queue message is push,
an ack message is sent for a queue message,
a cache key requested etc.

<br>

#### Transactions

Transactions are for distributing your operations between applications.
You can start a transaction in horse server and do your operation.
If your operation times out or your application crashed, horse triggers
event for transaction timeout operations. Or you can just commit, horse
triggers commit operations. Transactions can be chained and applications
do not require know about each other.

<br>

### Client Management and Authentication

As we discussed above in Direct Messaging topic, each client is just a client in horse
and they are manageable. You can implement your handlers for client operations.
If a client should be accepted, if a client requires a token, if a client is permitted to do the operation,
when a client is connected, which one is disconnected, etc.
You can implement your classes and rule everything about clients.

<br>

### Clustering and Nodes

Horse supports two kind of clustering mode.
- **Reliable,** duplicates all queues and messages to multiple servers. Only one node can be master at same time and all clients connects to master node. It's a good solution for persistent queue usages.
- **Scaled,** each server is independent but they sends direct and channel messages each other. And you can send a messages to a client which is connected to different server and get a response from it. And you can publish a message to a channel and the message is sent to all servers and all clients of all servers can receive the same message. If you want to broadcast messages to millions of clients at same time, that mode is a good choice.

Horse does not support one hostname which allows you to connect any of active server.
If you want that kind of solution, you can use different tool for it.
For clustering installation, servers should know endpoints of others.
And clients should know at least one active server's endpoint.
When a client is connected to an active server, all other server endpoints will be sent to the clients.
Horse clients supports multiple host endpoints to connect.
If you add all hosts into the clients, they always find a way to connect to the master node.

### Server Installation and Configuration

Horse Messaging Server runs on Horse Server. Horse Server is just a TCP server which handles clients and manges protocol implementations.
Horse Messaging Server is a library which has it's own Horse Protocol.
In order to install a server, we need to create a tcp server (Horse Server) and implement Horse Messaging (horse protocol) to the server.

Creating a server is simple.

```cs
HorseServer server = new HorseServer();
//do some implementations
server.Run(26200);
```

Now we need to configure Horse Messaging Protocol implementation.
The main manager object of the Horse Messaging Protocol is HorseRider class.
We strongly recommend to create HorseRider objects via HorseRiderBuilder class.
Simple usage:

```cs
HorseRider rider = HorseRiderBuilder.Create().Build();
```

And you can implement protocol with server.UseRider(rider) method.
But you always need some configurations. HorseRiderBuilder has good configuration methods.
You can check other methods of HorseRiderBuilder but let's look some mostly used configuration methods with full example code.

```cs
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.Push;
        cfg.UseMemoryQueues(c =>
        {
            c.Options.CommitWhen = CommitWhen.AfterReceived;
            c.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            c.Options.PutBack = PutBackDecision.Regular;
        });
    })
    .ConfigureChannels(cfg =>
    {
        cfg.Options.AutoChannelCreation = true;
    })
    .ConfigureCache(cfg =>
    {
        cfg.Options.DefaultDuration = TimeSpan.FromMinutes(30);
    })
    .Build();
    
HorseServer server = new HorseServer();
server.UseRider(rider);
server.Run(26200);
```