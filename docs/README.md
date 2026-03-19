# Horse Messaging Documentation

Horse Messaging is a high-performance, open-source messaging server and client library for .NET. It provides queues, channels, direct messaging, routers, caching, transactions, clustering, and a plugin system — all over a custom binary protocol.

---

## 1. [Queues](queues.md)

Message queues with multiple delivery patterns, acknowledgment strategies, persistent and in-memory storage, and partitioned queues for multi-tenant workloads.

- Queue Types (Push, Round Robin, Pull)
- Queue Options (Acknowledge, CommitWhen, Timeout, Limits, AutoDestroy, etc.)
- Server Configuration & Queue Event Handlers
- Storage Backends (Memory, Persistent, Custom Queue Managers)
- Producing & Consuming Messages
- Acknowledgment & Reliability
- Partitioned Queues (Labels, Auto-Assign Workers, Per-Tenant FIFO)
- Queue Topics & Topic Binding
- Queue Lifecycle (Auto Creation, Status, Auto Destroy, Configuration Persistence)

## 2. Channels

Pub/sub messaging channels. A producer publishes to a channel and all subscribed clients receive the message in real time. Unlike queues, channels do not store messages — delivery is fire-and-forget to currently connected subscribers.

- Channel Options (`HorseChannelOptions`)
- Auto Create & Auto Destroy
- Channel Status
- Subscriber Limit
- Send Latest Message on Subscribe
- Server Configuration (`ConfigureChannels`, `HorseChannelConfigurator`)
- Channel Event Handlers (`IChannelEventHandler`)
- Channel Authorization (`IChannelAuthorization`)
- Client-Side Publishing (`IHorseChannelBus`, `ChannelOperator`)
- Client-Side Subscribing (`IChannelSubscriber<TModel>`)
- Channel Subscriber Registration (Transient, Scoped, Singleton)
- Channel Attributes (`[ChannelName]`, etc.)
- Channel Configuration Persistence

## 3. Direct Messaging

Peer-to-peer messaging between clients. A client sends a message directly to another client identified by ID, name, or type. Supports request/response patterns.

- Direct Message Handlers (`IDirectMessageHandler`)
- Request/Response Handlers (`IHorseRequestHandler<TRequest, TResponse>`)
- Server Configuration (`ConfigureDirect`, `HorseDirectConfigurator`)
- Server-Side Handler (`IDirectMessageHandler`)
- Client-Side Sending (`IHorseDirectBus`, `DirectOperator`)
- Client-Side Receiving (`IDirectMessageHandler<TModel>`)
- Direct Handler Registration (Transient, Scoped, Singleton)
- Direct Attributes (`[DirectTarget]`, `[ContentType]`, etc.)
- Finding Clients by ID, Name, or Type

## 4. Routers

Message routers that decouple producers from consumers. A producer publishes to a named router, and the router forwards the message to one or more bindings based on the configured route method.

- Route Methods (Distribute, Round Robin, Only First)
- Binding Types:
  - `QueueBinding` — routes to a specific queue
  - `TopicBinding` — routes to queues matching a topic pattern
  - `DirectBinding` — routes to a client
  - `AutoQueueBinding` — auto-creates target queue
  - `DynamicQueueBinding` — routes by message target
  - `HttpBinding` — forwards to an HTTP endpoint
- Binding Interaction (None, Response)
- Router Configuration (`ConfigureRouters`, `HorseRouterConfigurator`)
- Router Message Handlers (`IRouterMessageHandler`)
- Client-Side Publishing (`IHorseRouterBus`, `RouterOperator`)
- Router Attributes (`[RouterName]`, etc.)
- Router Configuration Persistence
- Creating & Removing Routers and Bindings at Runtime

## 5. Cache

Built-in distributed key-value cache. Clients can set, get, and remove cache entries. Supports tags, TTL, size limits, and authorization.

- Cache Options (`HorseCacheOptions`)
  - Max Keys
  - Max Value Size
  - Default Duration
- Server Configuration (`ConfigureCache`, `HorseCacheConfigurator`)
- Cache Authorization (`ICacheAuthorization`)
- Client-Side API (`IHorseCache`)
  - Set, Get, GetOrSet, Remove
  - Increment, Decrement
  - Tags & Tag-Based Operations
  - Listing Cache Keys (with filter)
  - Persistent Cache Entries

## 6. Transactions

Server-side distributed transactions. A client begins a transaction, performs operations, and then commits or rolls back. The server tracks transaction state and can auto-timeout.

- Transaction Lifecycle (Begin, Commit, Rollback, Timeout)
- Transaction Status (None, Begin, Commit, Rollback, Timeout)
- Server-Side Endpoints (`IServerTransactionEndpoint`, `QueueTransactionEndpoint`)
- Server-Side Handlers (`IServerTransactionHandler`)
- Server Configuration (`TransactionRider`)
- Client-Side API (`HorseTransaction`)
  - Begin, Commit, Rollback
  - Auto-Dispose on Rollback

## 7. Client Management

Managing connected clients on the server. Clients have an ID, name, type, and token. The server can authenticate, authorize, and track clients.

- Client Properties (UniqueId, Name, Type, Token)
- Client Statistics (`ClientStats`)
- Client Authentication (`IClientAuthenticator`)
- Client Authorization (`IClientAuthorization`)
- Client Handlers (`IClientHandler`)
- Client Configuration (`ConfigureClients`, `HorseClientConfigurator`)
- Server-Side Client Limits (`HorseRiderOptions.ClientLimit`)
- Listing Connected Clients

## 8. Events

Server-side event system. Clients can subscribe to system events (queue created, message pushed, client connected, etc.) and receive real-time notifications.

- Event Subscriptions
- Event Types (Queue, Channel, Client, etc.)
- Client-Side Event Handling (`IHorseEventHandler`)
- Event Operator (`EventOperator`)
- Event Attributes

## 9. Clustering

Multi-node clustering for high availability and horizontal scaling. Supports main/replica topology with automatic failover.

- Cluster Modes (`Reliable`, `Publish`)
- Cluster Options (`ClusterOptions`)
- Node States (Main, Replica, Successor)
- Node Discovery & Connection
- Queue State Synchronization
- Cache State Synchronization
- Channel State Synchronization
- Router State Synchronization
- Replica Acknowledgment
- Failover & Main Node Announcement

## 10. Security & Authorization

Authentication and authorization at every level — client connection, queue access, channel access, cache access, and admin operations.

- Client Authentication (`IClientAuthenticator`)
- Client Authorization (`IClientAuthorization`)
- Queue Authentication (`IQueueAuthenticator`)
- Channel Authorization (`IChannelAuthorization`)
- Cache Authorization (`ICacheAuthorization`)
- Admin Authorization (`IAdminAuthorization`)

## 11. Protocol

Custom binary protocol (`Horse Protocol`) designed for low-latency, high-throughput messaging.

- Message Types (Server, QueueMessage, DirectMessage, Response, Acknowledge, Event, Ping/Pong, etc.)
- Message Structure (Frame, Headers, Content)
- Protocol Reader & Writer (`HorseProtocolReader`, `HorseProtocolWriter`)
- WebSocket Transport (`Horse.Messaging.Protocol.OverWebSockets`)
- Content Serialization (`IMessageContentSerializer`)
- Unique ID Generation (`IUniqueIdGenerator`)
- Known Content Types & Headers (`KnownContentTypes`, `HorseHeaders`)

## 12. Client Library

The .NET client library for connecting to a Horse Messaging server.

- `HorseClient` — Core client connection, send/receive, reconnect
- `HorseClientBuilder` — Fluent builder for DI registration
- Connection Management (Auto Reconnect, Graceful Shutdown)
- Client Operators:
  - `QueueOperator` — Queue operations (push, subscribe, create, remove, list)
  - `ChannelOperator` — Channel operations (publish, subscribe, list)
  - `DirectOperator` — Direct messaging (send, request/response)
  - `RouterOperator` — Router operations (publish, create, list bindings)
  - `EventOperator` — Event subscriptions
  - `ConnectionOperator` — Connection management
- Bus Interfaces (`IHorseQueueBus`, `IHorseChannelBus`, `IHorseDirectBus`, `IHorseRouterBus`)
- Interceptors (`IHorseInterceptor`)
- Auto Subscribe
- DI Integration (`Horse.Messaging.Extensions.Client`)
  - `IHostBuilder.AddHorse`
  - `IHostApplicationBuilder.AddHorse`
  - `IHost.UseHorse`
  - Graceful Shutdown (`GracefulShutdownOptions`)

## 13. Plugins

Dynamic plugin system for extending the server at runtime. Plugins are loaded from assemblies and can access queues, channels, cache, and clients.

- Plugin Configuration (`ConfigurePlugins`, `HorsePluginConfigurator`)
- Plugin API (`PluginRider`, `PluginQueue`, `PluginChannel`, `PluginMessagingClient`)
- Plugin Queue Rider & Cache Rider
- Assembly Loading (`PluginAssemblyLoadContext`)

## 14. Data & Persistence

Persistent storage layer for queues using the HDB file format.

- `Horse.Messaging.Data` Package
- HDB File Format (append-only, auto-shrink, backup)
- Persistent Queue Manager
- Data Configuration (Flush Strategy, Auto Shrink, Backup)
- Redelivery Tracking & Headers

---

## NuGet Packages

| Package | Description |
|---------|-------------|
| `Horse.Messaging.Server` | Server library — queues, channels, routers, cache, clustering |
| `Horse.Messaging.Client` | Client library — connect, produce, consume, direct messaging |
| `Horse.Messaging.Protocol` | Wire protocol — message types, reader/writer, headers |
| `Horse.Messaging.Protocol.OverWebSockets` | WebSocket transport layer |
| `Horse.Messaging.Data` | Persistent storage (HDB) for queues |
| `Horse.Messaging.Extensions.Client` | DI integration for .NET Generic Host |
| `Horse.Messaging.Plugins` | Plugin SDK for extending the server |

