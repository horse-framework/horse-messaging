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

* First or all, **Horse Messaging is a framework, not an application.**
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
