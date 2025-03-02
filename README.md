<p align="center">
<picture>
  <source media="(prefers-color-scheme: light)" srcset="https://avatars.githubusercontent.com/u/14873804?s=400&u=b0533ffa4ca335a2d497b648e9b13a0d0885cac2&v=4">
  <source media="(prefers-color-scheme: dark)" srcset="https://avatars.githubusercontent.com/u/14873804?s=400&u=b0533ffa4ca335a2d497b648e9b13a0d0885cac2&v=4">
  <img src="https://avatars.githubusercontent.com/u/14873804?s=400&u=b0533ffa4ca335a2d497b648e9b13a0d0885cac2&v=4" alt="Arad ITC Logo" width="30%"> 
</picture>
</p>

# Horse Messaging

[![NuGet](https://img.shields.io/nuget/v/Horse.Messaging.Server?label=Server%20NuGet)](https://www.nuget.org/packages/Horse.Messaging.Server)
[![NuGet](https://img.shields.io/nuget/v/Horse.Messaging.Client?label=Client%20NuGet)](https://www.nuget.org/packages/Horse.Messaging.Client)
[![NuGet](https://img.shields.io/nuget/v/Horse.Messaging.Plugins?label=Plugins%20NuGet)](https://www.nuget.org/packages/Horse.Messaging.Plugins)
[![NuGet](https://img.shields.io/nuget/v/Horse.Jockey?label=Jockey%20Panel%20NuGet)](https://www.nuget.org/packages/Horse.Jockey)

## What's Horse Messaging

Horse Messaging is a communcation framework. It provides many features.
All features can be used over only once client and one connection full asynchronously. 

* Push State Messaging Queues (supports persistent queues)
* Pull State Messaging Queues (supports persistent queues)
* Message Broadcasting over Channels
* Distributed Cache Management
* Direct Messaging Between Clients
* Proxy for Request and Response Messaging
* Remote Transactions
* Event Management
* Message Routing

## Why should I use it ?

* First of all, **Horse Messaging is a framework, not an application.**
  That gives you unlimited customization opportunity. 
  Horse Messaging Server provides you many many implementation options to customize everything in it. 
  On the other hand, if you want to use Horse Messaging Server with default implementations, 
  you can create very basic application with a few lines of code.
  
  
* **It's a complete communication framework.**
  It's a bridge between your applications.
  It's not just messaging queue or cache server.
  Horse gives you unlimited communication possibilities.
  You can use all kind of messaging architectures with same code base.


* **It's extremely extensible and customizable.**
  Everything has an implementation and all operations are interceptable.
  You can even use your custom SQL server to make your queues durable.
  

* **It's fast, uses low memory and cpu.**
  Queues can handle over 200k messages per second,
  Channels can handle over 350k messages per second.
  There is no delay in Horse, latency depends on your network connection.


* **C#/.NET Core-Powered Optimized.**
  for cloud-native and on-prem environments.
  
* **Resilient & Fault-Tolerant.**
  Ensures stability even under extreme workloads.

  
* **Open-Source & Developer-Friendly.**
  Encouraging innovation and collaboration.

  
* **Scalable & Cost-Effective.**
  Ideal for large-scale deployments with minimal infrastructure costs.

  
* **Powerful Server-Side Customization**.
  Through interface-based implementations, Horse provides extreme flexibility for customization on the server side, a feature none of the other tools offer.

  
* **Client-to-Client Direct Communication**.
   Unlike any other tool, Horse enables direct client-to-client communication without requiring a queue or pub/sub mechanism.

  

## Horse Features with Comparison 

|**Feature** |**Horse** |**Rabbit MQ** |**Kafka** |**Redis** |
| :- | :- | :- | :- | :- |
|Messaging Queues|<p>- Push and Pull state queues </p><p>- Distribute, Round Robin, Only First algorithms </p><p>- Same client publisher/consumer </p><p>- Same client multiple queues </p><p>- Memory & Persistent Queues </p><p>- Priority and regular messages in same queues </p><p>- Custom Persistence implementation </p><p>- Custom algorithms supported </p><p>- Publishing to routers supports multiple algorithms </p><p>- Advanced Commit options </p><p>- FIFO guarantee </p><p>- At Most Once, At Least Once, Exactly Once </p><p>- Advanced Acknowledge options </p><p>- Semi-Efficient message batching </p><p>- Multiple put back solutions for unacked messages </p><p>- Multiple auto destroy options </p><p>- Message TTL and Acknowledge timeouts </p><p>- Allowed maximum consumer management </p><p>- Message and size management </p><p>- Delaying between consume operations management </p><p>- Advanced status management (run, pause, only push, only consume) </p><p>- Clear and Move all messages </p><p>- Low consumer latency </p><p>- Up to 180k msg/sec on each memory queue </p><p>- Up to 140k msg/sec on each persistent queue </p>|<p>- Only Push states queues </p><p>- Many queue types for memory/persistence and auto destroy operations such as Durable, Exclusive, Transient. </p><p>- Advanced Acknowledge options </p><p>- Moving messages with dead lettering </p><p>- Message TTL </p><p>- Queue Limits </p><p>- FIFO guarantee </p><p>- At Most Once, At Least Once </p><p>- Inefficient message batching </p><p>- Consumer prefetch management </p><p>- Priority queues </p><p>- Delayed messages </p><p>- Low consumer latency </p><p>- Up to 55k msg/sec on each persistent queue </p><p>&emsp;(with 15 secs spike for each a few min) </p>|<p>- Only Pull state queues </p><p>- Distributing messages into partitions in different servers </p><p>- FIFO guaranteed only in same partitions </p><p>- Consumer groups </p><p>- Message retentions </p><p>- Only persistent </p><p>- Key based partitioning </p><p>- Message offset management </p><p>- Compressions </p><p>- Efficient message batching </p><p>- Each client can subscribe to only one topic </p><p>- High throughput & low latency or up to 1 second consume latency </p><p>- At Most Once, At Least Once, Exactly Once </p><p>- Performance based on network speed instead of message count and it’s up to server’s network speed. </p>|Not Supported |
|Channels & Pub/Sub|<p>- Extremely low latency </p><p>- Subscriber initial messages </p><p>- Subscriber management </p><p>- Unlimited channel subscription for each client </p><p>- Auto create / destroy options </p><p>- Client and Message Size limits </p><p>- Topic bindings </p><p>- Nearly 1M messages for each channel </p>|Not Supported |Not Supported |<p>- Extremely low latency </p><p>- Auto create / destroy </p><p>- Wildcard subscription </p>|
|Client Management|Fully supported, any client with permissions can monitor and manage others |Not Supported |Limited monitoring **of** Admin Clients |Not Supported |
|Distributed Caching|<p>- Get, Set, Refresh, TTL </p><p>- Soft & Hard TTL Management </p><p>- Alerts to receivers for expirations </p><p>- Incremental cache values for rate limiting solutions </p><p>- Category tagging </p><p>- String and Binary support. </p><p>- Over 1M op/sec. </p>|Not Supported |Not Supported |<p>- Get, Set, Refresh, TTL </p><p>- Advanced TTL Management </p><p>- Incremental cache values for rate limiting solutions ● Category tagging. </p><p>- Advanced category and type management. </p><p>- String and Binary support. </p><p>- Cache Persistence </p><p>- Over 3M op/sec </p>|
|Client Messaging|<p>- Client can send messages each other </p><p>- Client can send messages to a group of clients with type and name </p><p>- Clients and sends requests and waits for response </p><p>- Client messaging can be managed with different algorithms (send all, only one, only first etc.) </p>|Supported only over queues with the same algorithm messaging queues. |Not Supported |Not Supported |
|Routers|<p>- Distribute, Round Robin and Only First options </p><p>- You can route messages with different targets in same route; Queue, Topic, Dynamic, Client, even HTTP Request to external API </p><p>- Binding priority management </p><p>- Binding interaction options </p>|<p>- Supported in only queues with Exchanges </p><p>- Multiple algorithms supported such as </p><p>&emsp;Fanout, Direct </p>|Not Supported |Not Supported |
|Client Supports|C#, Java, Python, Javascript |<p>C#, Java, Python, Javascript, PHP, Ruby, Go, </p><p>C/C++, Elixir, Erlang </p>|<p>C#, Java, Python, Javascript, Go, C/C++, </p><p>Ruby, Scala </p>|<p>C#, Java, Python, Javascript, PHP, </p><p>Go, Ruby, C/C++, Rust </p>|
|Customization|Unlimited, can you develop your own plugins and intercept everything on both server and client side |Has some useful plugins and may be developed |Not explicitly supported some solutions might be found |LUA Scripting on server side |
|Overall|Powerful, Flexible, Efficient, Feature Rich, All in One |Flexible, Feature Rich, may require more resources (especially RAM) |Powerful, Strict, Efficient |<p>Powerful, Limited use cases, </p><p>Efficient </p>|

## Horse use cases
**1. Financial Transaction Processing in Payment Systems**

**Horse** can be used as a Message Broker in online payment systems for processing transactions, confirmations, and notifications to users. In this scenario:

- Transactions are sent as messages to **Horse** from different systems.
- **Horse** forwards these messages to other systems like payment gateways, banking systems, or payment platforms.
- The features of **remote transactions** and **message routing** are used for coordinating between multiple services.

**2. Complex Event Management (Event-Driven Architectures)**

In systems relying on **Event-Driven Architectures**, **Horse** can be used to manage events. For example:

- In an e-commerce system, events such as "order placed," "payment successful," "item shipped," etc., are sent as messages to **Horse**.
- **Horse** forwards these events to various systems (like inventory, shipping, customer service) for further processing.
- The **event management** and **message broadcasting** features are used to broadcast events simultaneously to different systems.

**3. Messaging Platforms and Communication Systems**

In messaging platforms like SMS or chat systems, **Horse** can be used as a Message Broker to send messages simultaneously to thousands or millions of users:

- Messages are broadcast from a single source to thousands of destinations using **message broadcasting**.
- The **message delivery quality** and **persistent queues** ensure the reliable sending of messages even during network issues.
- Direct messaging or **proxy messaging** is available for special cases requiring authentication or filtering.

**4. Data Management in Microservices Systems**

In a **microservices architecture**, where multiple independent services are connected, **Horse** can be used as a Message Broker for sending data and coordinating between services:

- Each service can independently send data via messages to other services.
- **Horse** allows easy scaling of this message exchange, handling large numbers of services with its **scalability** and **flexibility**.
- **Message routing** ensures that messages are delivered to the correct service while maintaining high performance.

**5. Notification and Alert Systems**

In notification and alert systems, like monitoring systems, **Horse** can be used to send alerts and notifications to system admins and users:

- Whenever a critical event or alert occurs in the system, the message is sent to **Horse**.
- **Horse** forwards the message to multiple recipients (via email, SMS, or apps) simultaneously.
- **Event management** and **message broadcasting** are used to quickly send these alerts to the relevant parties.

**6. IoT (Internet of Things) Systems**

In **IoT** systems, where devices and sensors continuously generate data, **Horse** can be used to manage messages and data between devices and central servers:

- Data generated by sensors is sent to **Horse**.
- **Horse** forwards the messages to data processing, analytics, or storage systems.
- The **event management** and **distributed cache management** features are used to store and quickly access data.

**7. Multi-Layered System Development and Interoperability**

In a multi-layered system where multiple modules (e.g., databases, various services, and analytics systems) need to interact, **Horse** can act as the communication bridge between them:

- Each module can send messages to **Horse**, which then forwards them to other modules or services.
- This process ensures reliable message delivery through **persistent queues** and maintains operation even when certain systems are temporarily unavailable.

## Quick Start

Install and run basic server application

```docker run -p 2626:2626 -p 2627:2627 horseframework/messaging-server```

Create new .NET Core project and add Horse.Messaging.Client nuget package into your project.

```cs
HorseClient client = new HorseClient();
await client.ConnectAsync("horse://localhost:2626");
```

Navigate to Jockey panel http://localhost:2627 with empty username and password.
You can see your connected client in clients page.


[![jetbrains](https://user-images.githubusercontent.com/21208762/90192662-10043700-ddcc-11ea-9533-c43b99801d56.png)](https://www.jetbrains.com/?from=horse-framework)
