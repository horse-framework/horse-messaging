# Horse Messaging Documentation

[Back to Documentation Contents](README.md)

## Queues

Queues are for ordering messages and consuming them with consumer speed.
Horse Queues have some capabilities and limitations.
Let's a quick look for horse queues:

- Each queue is atomically first in first out (FIFO)
- One or multiple producers can produce messages into same queue. There is no producer limitation for queues.
- One or multiple consumers can consume from same queue. There is no consumer limitation for queues. (if you don't set
  in options)
- There is no queue limit in server. If you have enough resources (disk, ram, cpu) you can create unlimited queues.
- Depends on your queue state and persistent/memory storing option, each queue can handle up to 300k messages per
  second.
- All queue messages are kept in memory even they are persistent queues, you always need enough memory. But no need to
  worry, eveny millions of messages take up less than 1 GB of space.
- Messages are not duplicated for each consumer. Only one acknowledge is required for each message. If you want to
  duplicate messages into multiple queues you should use routers.
- Everything is intercept-able and open to implementation in queue structure.

### Queue Options

Each queue has it's own options. And server has a default queue options.
Default options applied firstly when a queue is created.
After that, specified options while creating queue options are applied.
Here the list of all queue options.

| Option                | Type     | Description                                                                                                                                                                                                                                                                                                                                 |
|-----------------------|----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Acknowledge           | Enum     | When a consumer consumed a message, pending ack option from the consumer. **None** for no ack. **JustRequest** is ack required for removing message but queue keeps sending next messages when ack is not received. **WaitForAcknowledge** waits the ack message and do not process next messages in queue until ack received or timed out. |
| CommitWhen            | Enum     | When a producer produces a message, pending commitment from the server, if the message is received or not.                                                                                                                                                                                                                                  |
| AcknowledgeTimeout    | TimeSpan | **AfterReceived** commit is sent when message received by the server (and saved if persistent). **AfterSent**, after message is sent at least one active consumer. **AfterAcknowledge**, after a consumed sent ack message for the message.                                                                                                 |
| MessageTimeout        | TimeSpan | Message timeout duration. Zero for unlimited. Message is removed from the queue when it's expired, even if it is not consumed.                                                                                                                                                                                                              |
| Type                  | Enum     | Push, Pull or RoundRobin. All states are described below.                                                                                                                                                                                                                                                                                   |
| MessageLimit          | Integer  | Queue message limit. Zero is unlimited.                                                                                                                                                                                                                                                                                                     |
| LimitExceededStrategy | Enum     | When producer tries to produce a message into a full queue (limited by MessageLimit property). A decision, what should be done. **RejectNewMessage** or **DeleteOldestMessage.**                                                                                                                                                            |
| MessageSizeLimit      | Integer  | Unsigned Int64 size limit for each message in bytes.                                                                                                                                                                                                                                                                                        |
| ClientLimit           | Integer  | Consumer client limit for the queue. Zero is unlimited.                                                                                                                                                                                                                                                                                     |
| DelayBetweenMessages  | Integer  | In some situations (such as JustRequest acknowledge option) consumers can go overheat easily. Server sends thosands of message per secs. That option in milliseconds, delay between two messages. If you set that value 25, server waits for 25m before sending next messages to any consumer. Default value is zero.                       |
| PutBack               | Enum     | When a message consume operation failed, timed out or negative ack received. Horse puts the message back into the queue. That option decision for it. **No** throws the message, it's destroyed. **Priority** message will be consumed first. **Regular**, message is consumed last.                                                        |
| PutBackDelay          | Integer  | In some situations server and client can overheat, negative ack received, put back prority, again negative ack received etc. That option prevents overtheat and puts message into the queue after specified milliseconds later.                                                                                                             |
| AutoDestroy           | Enum     | Auto destroy options for the queue. Default value is **Disabled.** **NoMessages**, queue is destroyed when empty even it has active consumers. **NoConsumers**, queue is destroyed when there is no active consumers, even it has messages. **Empty**, queue is destroyed but there is no messages and active consumers.                    |
| AutoQueueCreation     | Boolean  | It's server global option. If true, server auto created queue when a client produces or subscribes to a non-exists queue.                                                                                                                                                                                                                   |
| MessageIdUniqueCheck  | Boolean  | Each queue message should be unique. By default, client sends the messages with a unique id. But you can manually set a unique message id too. If you are unsure if your manual id is not unique or should not duplicate, you can enable that option. But performance greatly decreases. (~0.3 ms for each message)                         |

There are multiple ways to initialize queue with options. The lowest priority is default options of server.
Here is the sample code how you can define default queue options in your server.

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.Push;
        cfg.Options.MessageLimit = 400;
    })
    .Build();
```

When a queue is created, firstly, default options are applied described above.
But if you create a new queue in server side with Create method of QueueRider class, it depends which overload you are using.
If your chosen overload requires instance of an options class, default options are by-passed.
But if your chose overload requires an action of options, default options are applied firstly, then your action is invoked.
If your chosen overload does not require options instance or action, queue will be created with exactly default options.

Another way of setting options is sending options data from client to server.
HorseClient object has a Queue property, represents queue operator for the client.
That object has SetOptions method. Here is the sample code:

```csharp
HorseClient client = new HorseClient();
await client.ConnectAsync("horse://localhost:port");
await client.Queue.SetOptions("queueName", opt =>
{
    opt.DelayBetweenMessages = 10;
});
```

And there are some other ways to change (actually to initialize) options of a queue that are explained in Creating Queues section below. 

### Creating Queue

Creating queue from both server and client is possible. We won't discuss creating queues from server side in here.
In most situations, we need to create queues over clients. And sometimes from an administration panel, you can use Horse Jockey it.

There is three kind of queue creation over clients.

**1. Creating queues directly**<br>
It's via calling create method of queue operator. It initializes the queue with specified options.

**2. Creating queues with produce operation.**
When a producer client pushes a message into a queue that is not exists,
if AutoQueueCreation option is enabled (server side option, and it's true by default) queue is created automatically.
And that operation initializes queue also.

**3. Creating queues with subscription request.**
When a consumer client subscribes to the queue.
If AutoQueueCreation option is enabled queue is created automatically.
But queue is not initialized, queue state is not decided and it's queue manager are not set unless consumer class has QueueType attribute.

#### What is Queue Initialization?

Queue initialization is deciding queue type (push, pull, round robin) and doing first operations before produce, consume operations starts.
It also includes loading messages.

Queue initialization is always done if a queue is already created before server restart, or queue is created manually etc.
There is only one case that queue is created but not initialized: a client subscribes to a non-exist queue and queue is created automatically.
In many cases, consumer can subscribe before producer pushes it's first message. So, to complete the subscription operation queue must be created.
But queue should not be initialized at that step. Because consumers are passive by their operations. The message owner (producer client) should decide what kind of queue it will be.
The producer says the last words about queue type and queue manager. Especially QueueManager changes everything about the queue, if it's persistent and or not, and the message owner should decide.
After consumer subscribes to the queue, queue status will be Not Initialized. With first message of producer, queue is initialized.

But in some cases, you might want to decide the queue type or manager by consumer client. If you add QueueType attribute to the consumer,
server understands the request of your queue initialization and initializes the queue automatically even it's created by consumer subscription message.

In conclusion, mostly you do not use Create method of queue operators and manage your queue creations automatically by producer and consumer attributes.
Here are sample codes:


**Creating Queue From Client**

```csharp
HorseClient client = new HorseClient();
await client.ConnectAsync("horse://localhost:port");
client.Queue.Create("QueueName", opt =>
{
    /* override some options */
});
```

**Creating Queue By Producing Message**

```csharp
[QueueName("demo-queue")]
[QueueType(MessagingQueueType.Push)]
public class Model
{
    public string Foo { get; set; }
}

HorseClient client = new HorseClient();
await client.ConnectAsync("horse://localhost:port");
client.Queue.PushJson(new Model { /* stuff .. */ });
```

**Creating Queue By Consuming Message**

```csharp
[QueueName("demo-queue")]
[QueueType(MessagingQueueType.Push)]
public class Model
{
    public string Foo { get; set; }
}

//It's possible to set attributes here, instead of model. Both acceptable.
public class ModelConsumer : IQueueConsumer<Model>
{
    public Task Consume(HorseMessage message, Model model, HorseClient client)
    {
        throw new System.NotImplementedException();
    }
}

HorseClient client = new HorseClientBuilder()
    .AddHost("horse://localhost:post")
    .AddSingletonConsumer<ModelConsumer>()
    .Build();

await client.ConnectAsync();
```

**NOTE:** It's okay if both applications (consumer and producer) have same attributes. That does not cause duplicate queue create options or another problem. Actually, it's the recommended usage. But if they have different values of options, creator (usually first connected) client's options are set.

### Queue States

Horse supports three queue state types. Each state is for different use case.
Queue states can be assigned with queue options or with attribute from clients.
Queue state must be set before queue initialization. The state cannot be changed if the queue is initialized.

#### Push State

Push state queues send the produced message to the consumers when they are ready.
If there are online consumers, that operation is done right after the message is produced.
If multiple consumers are subscribed to the queue, the message is sent to ALL consumers at same time.
Acknowledge operations are done when at least one consumer sends acknowledge message.

#### Round Robin State

Round robin state works similar to push state queues. But each message is sent to only one consumer.
The chosen consumer is always next available client.
If multiple consumers are available, oldest (firstly connected) client is chosen.

#### Pull State

Pull state queues are designed to consume messages when consumer is available for the operation.
In order to consume messages in Pull state queues, client should send a pull request, otherwise messages stay in queue forever (if there is no message timeout).
In pull state queues, each message is sent to only one consumer at same time.

### Memory and Persistent Queues

Technically, it's possible and easy to create your own queue management system.
And there might unlimited kind of queues in same server at same time.
By default, Horse supports two kind of queues: Memory and Persistent.
Memory queues keeps all data only in memory and the queue messages are lost when server restarts.
Horse's default persisent queues keeps messages in HQFS (Horse Queue File System).
Both queues keeps all messages in memory.
Even you are using persistent queues, Horse reads all messages from disk and keeps your messages in memory too.

#### Horse Queue File System

HQFS keeps messages in a file as binary data.
Message consume operations write a new flag for the message, instead of finding and replacing the data.
That system gives you consuming performance on persistent queues.
And a shrink operation works always in periodically.
It shrinks your queue file on disk. Shrink operation may cause queue lock for 500 milliseconds.
It's recommended to keep shrink period options above 60 seconds or more. It's 15 minutes by default.
If you increase the duration, you might require more disk space depending on the number of messages processed in the queue.

### Queue Infrastructure

Each queue is represented by a C# class with some components. Each component is usually implements an interface or base class.
These components consists of two main topics: Queue States and Queue Managers.

#### Queue States

Queue states manage how messages are being processed. There are default queue states Push, Pull and Round Robin.
Each queue state implements IQueueState interface. If you want to create your own queue state, you must implement that interface.

#### Queue Managers

Queue Manager is an implementation. Each queue manager must implement IHorseQueueManager interface.
A queue manager includes message stores, queue synchronizer, queue delivery handler and some other functionalities.

Each queue store implements IQueueMessageStore interface. That store determines how the message will be stored, in memory,
in file or sql server etc.

Queue synchronizer must implement IQueueSynchronizer interface. It manages how messages are being synced between cluster nodes.
If you do not have really good reason, we strongly recommend to use default synchronizers.

Queue delivery handlers implement IQueueDeliveryHandler interface. That handlers manage lifecycle of a queue message.
Interface has some event methods returns decisions about the events.

### Event Handlers

Subscribing Horse event from client is possible but we will describe server side event handlers in this section.
In order to watch the events from a client, you should read Events topic.

All Horse components supports event handler. For queue events you can use IQueueEventHandler implementation.
There are 5 different events in that interface:

1. **OnCreated :** Executed when a new queue is created.
2. **OnRemoved :** Executed when a queue is removed.
3. **OnConsumerSubscribed :** Executed when a client is subscribed to a queue.
4. **OnConsumerUnsubscribed :** Executed when a client is unsubscribed from a queue.
5. **OnStatusChanged :** Executed when a queue status changed.

When you implement your own class you should register your class to Horse Server.

```csharp
HorseRider rider = HorseRiderBuilder.Create().Build();
rider.Queue.EventHandlers.Add(new YourEventHandler());
```

An addition, there is one more event implementation for queues. That implementation for queue messaging events.
You must implement IQueueMessageEventHandler interface. That interface has 5 different events:

1. **OnProduced :** Executed when a new message is produced to a queue.
2. **OnConsumed :** Executed when a message is consumed from a queue.
3. **OnAcknowledged :** Executed when a consumer sends an acknowledge for a consumed message.
4. **MessageTimedOut :** Executed when a message is expired (not consumed) and removed from a queue.
5. **OnAcknowledgeTimedOut :** Executed when a consumer consumed message and did not send acknowledge message.

And registering that event handler to Horse is similar.

```csharp
HorseRider rider = HorseRiderBuilder.Create().Build();
rider.Queue.MessageHandlers.Add(new YourMessageEventHandler());
```
