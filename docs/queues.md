# Queues

Horse Messaging provides a powerful, flexible message queue system with support for multiple delivery patterns, acknowledgment strategies, in-memory and persistent storage backends, and partitioned queues for multi-tenant workloads.

## Table of Contents

1. [Overview](queues/overview.md)  
   What queues are, core concepts (producer, consumer, message), and how they fit into the Horse Messaging architecture.

2. [Queue Types](queues/queue-types.md)  
   Delivery patterns and when to use each one.
   - Push
   - Round Robin
   - Pull

3. [Queue Options](queues/queue-options.md)  
   Full reference of every configurable option on a queue.
   - Acknowledge Decisions (None, JustRequest, WaitForAcknowledge)
   - Commit Strategies (None, AfterReceived, AfterSent, AfterAcknowledge)
   - Acknowledge Timeout
   - Message Timeout & Timeout Policies
   - Message Limit & Limit Exceeded Strategy
   - Message Size Limit
   - Client Limit
   - Delay Between Messages
   - Put Back Decision & Delay
   - Auto Destroy (Disabled, NoMessages, NoConsumers, Empty)
   - Auto Queue Creation
   - Message ID Unique Check

4. [Server Configuration](queues/server-configuration.md)  
   Setting up queues on the server side.
   - Default Queue Options via `HorseRiderBuilder`
   - Creating Queues Programmatically with `QueueRider`
   - Queue Event Handlers (`IQueueEventHandler`)
   - Queue Message Event Handlers (`IQueueMessageEventHandler`)
   - Queue Authenticators (`IQueueAuthenticator`)

5. [Storage Backends](queues/storage-backends.md)  
   Choosing and configuring the message storage layer.
   - Memory Queues (`UseMemoryQueues`)
   - Persistent Queues (`UsePersistentQueues`)
   - Data Configuration (flush strategies, auto-shrink, backup)
   - Redelivery Tracking
   - Custom Queue Managers (`UseCustomQueueManager`)

6. [Producing Messages](queues/producing-messages.md)  
   Sending messages into queues from the client side.
   - Using `IHorseQueueBus`
   - Push with Headers
   - Push to Named Queues
   - Model-Based Publishing (Attributes)
   - Commit Behavior and Producer Acknowledgment

7. [Consuming Messages](queues/consuming-messages.md)  
   Receiving and processing messages on the client side.
   - `IQueueConsumer<TModel>` Interface
   - Consumer Lifecycle (Transient, Scoped, Singleton)
   - Registering Consumers via `HorseClientBuilder`
   - Auto Subscribe
   - Manual Subscribe & Unsubscribe
   - Pull Requests (`PullContainer`)
   - Consumer Attributes (`QueueName`, `QueueType`, `Acknowledge`, etc.)

8. [Acknowledgment & Reliability](queues/acknowledgment.md)  
   Ensuring messages are processed reliably.
   - Acknowledge Flow (Positive / Negative)
   - Auto Acknowledge
   - Put Back Behavior on Negative Ack & Timeout
   - Commit When (Producer-Side Confirmation)
   - Redelivery Headers

9. [Retry & Redelivery](queues/retry-redelivery.md)  
   Handling transient failures with client-side retry and server-side delivery tracking.
   - Client-Side Retry (`[Retry]` Attribute)
   - Retry Count, Delay, and Ignored Exceptions
   - Retry Interaction with Other Attributes
   - Server-Side Redelivery Tracking (`useRedelivery`)
   - Delivery Count Header
   - Redelivery Persistence and Lifecycle
   - Using Retry and Redelivery Together

10. [Dead-Letter Queues & Exception Handling](queues/dead-letter-exceptions.md)  
    Routing failed messages and exception data when consumers throw.
    - Exception Handling Pipeline Overview
    - `[MoveOnError]` — Moving Messages to Dead-Letter Queues
    - `[PushExceptions]` — Pushing Exception Models to Queues
    - `[PublishExceptions]` — Publishing Exception Models to Routers
    - Type-Specific Exception Routing
    - `ITransportableException` Interface and `ExceptionContext`
    - `ExceptionDescription` Metadata
    - `[AutoNack]` and `NegativeReason` Options
    - Combining Retry, MoveOnError, PushExceptions, and AutoNack
    - Recommended Patterns (DLQ, Centralized Logging, Poison Message Detection)

11. [Partitioned Queues](queues/partitioned-queues.md)  
    Scaling queues with label-based partitioning for multi-tenant and high-throughput scenarios.
    - Enabling Partitions (`PartitionOptions`)
    - Partition Labels
    - Subscribing with Labels
    - Auto Assign Workers (Worker Pool)
    - Max Partitions Per Worker
    - Partition Auto Destroy
    - Partition Metrics

12. [Queue Topics](queues/queue-topics.md)  
    Topic-based routing via the Router system.
    - Setting Topics (server-side, `Queue-Topic` header, `[QueueTopic]` attribute)
    - Topic Binding in Routers
    - Wildcard Pattern Matching
    - Topic Persistence

13. [Queue Lifecycle](queues/queue-lifecycle.md)  
    How queues are created, managed, and destroyed at runtime.
    - Auto Creation
    - Status (Running, Paused, Stopped)
    - Auto Destroy Rules
    - Destroying Queues Programmatically
    - Queue Configuration Persistence (JSON)

