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
