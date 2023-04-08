## Horse Messaging Documentation

## Server

- [Overview](overview.md)
    - [Horse Server and Riders](overview.md#horse-server-and-riders)
      - [Queues](overview.md#queues)
      - [Channels](overview.md#channels)
      - [Routers](overview.md#routers)
      - [Direct Messaging](overview.md#direct-messaging)
      - [Caching](overview.md#caching)
      - [Events](overview.md#events)
      - [Transactions](overview.md#transactions)
    - [Client Management and Authentication](overview.md#client-management-and-authentication)
    - [Clustering and Nodes](overview.md#clustering-and-nodes)
    - [Server Installation and Configuration](overview.md#server-installation-and-configuration)
<br><br>

- [Queues](queues.md)
    - [Queue Options](queues.md#queue-options)
    - [Creating Queue](queues.md#creating-queue)
    - [What is Queue Initialization?](queues.md#what-is-queue-initialization)
    - [Creating Queue From Client](queues.md#creating-queue-from-client)
      - [Creating Queue By Producing Message](queues.md#creating-queue-by-producing-message)
      - [Creating Queues By Consuming Message](queues.md#creating-queue-by-consuming-message)
    - [Queue States](queues.md#queue-states)
      - [Push State](queues.md#push-state)
      - [Round Robin State](queues.md#round-robin-state)
      - [Pull State](queues.md#pull-state)
    - [Memory and Persistent Queues](queues.md#memory-and-persistent-queues)
      - [Horse Queue File System](queues.md#horse-queue-file-system)
    - [Queue Infrastructure](queues.md#queue-infrastructure)
      - [Queue States](queues.md#queue-states-1)
      - [Queue Managers](queues.md#queue-managers)
    - [Event Handlers](queues.md#event-handlers)
<br><br>

- [Routers](routers.md)
    - [Bindings ad Binding Types](routers.md#bindings)
    - [Creating Routers and Bindings](routers.md#creating-routers-and-bindings)
    - [Publishing Messages to Routers](routers.md#publishing-messages-to-routers)
<br><br>

- [Channels](channels.md)
    - [Overview](channels.md#overview)
    - [Options](channels.md#options)
    - [Event Handlers](channels.md#event-handlers)
    - [Authorization](channels.md#authorization)
    - [Rule Everything](channels.md#rule-everything)
<br><br>

- [Cache](cache.md)
    - [Overview](cache.md#overview)
    - [Options](cache.md#options)
    - [Set and Get](cache.md#set-and-get)
    - [Authorization](cache.md#-authorization)
    - [Rule Everything](cache.md#rule-everything)
<br><br>

- [Direct](direct.md)
    - [Overview](direct.md#overview)
    - [Finding Target](direct.md#finding-target)
    - [Responses](direct.md#responses)
    - [Rule Everything](direct.md#rule-everything)
<br><br>

- [Client Management](client-management.md)
    - [Overview](client-management.md#overview)
    - [Authentication](client-management.md#authentication)
    - [Authorization](client-management.md#authorization)
    - [Administration](client-management.md#administration)
<br><br>
 
- [Clustering](clustering.md)
    - [Overview](clustering.md#overview)
    - [Distributed](clustering.md#distributed)
    - [Reliable](clustering.md#reliable)
<br><br>

## Client

- [Simple Usage and Quick Installation](client.md#quick-installation)
- [Client Options](client.md#options)
- [Client Operators](client.md#operators)
- [Using with Microsoft Dependency Injection](client.md#using-msdi)
- [Using with Microsoft Hosting Extension](client.md#using-hosting-extension)
- [Sending and Receiving Messages](client.md#sending-and-receiving-messages)
- [Using Annotations](client.md#using-annotations)
- [Subscribe to Server Events](client.md#subscribe-to-server-events)
- [Unique Id Generator](client.md#unique-id-generator)
- [Message Serialization](client.md#message-serialization)
