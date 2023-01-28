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

### Creating Queues

### Queue States

#### Push State

#### Round Robin State

#### Pull State

### Memory and Persistent Queues

### Infrastructure

### Event Handlers