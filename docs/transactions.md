# Transactions

A transaction lets a client hand a payload to the server and decide later whether it is committed or rolled back. The server holds the payload while the transaction is open. When the transaction finishes, the server delivers the payload to the endpoint of the outcome: the commit endpoint, the rollback endpoint or the timeout endpoint.

Transactions of the same kind belong to a **transaction container**. A container has a name, a timeout and its endpoints. Clients begin transactions by container name.

## Lifecycle

```
Begin ──► open ──┬── Commit by owner ───────────► commit endpoint
                 ├── Rollback by owner ─────────► rollback endpoint
                 ├── Deadline passed ───────────► timeout endpoint (rollback endpoint if none)
                 ├── Owner client disconnects ──► rollback endpoint
                 ├── Server shutdown ───────────► rollback endpoint
                 └── Container deleted ─────────► rollback endpoint
```

- **A transaction finishes exactly once.** Commit, rollback, timeout, disconnect, shutdown and container delete all compete to finish an open transaction. Only one of them wins. The others see that the transaction is not open anymore.
- **Finished transactions are not kept.** When a transaction finishes, it is removed from the server. Later requests for the same transaction receive `NotFound`.
- **Only the owner can finish a transaction.** The owner is the connection that began the transaction. A request from another client receives `NotFound`, the same answer as for an unknown transaction.
- **A transaction lives only as long as its connection.** If the owner disconnects, the server rolls back the owner's open transactions. A transaction does not survive a reconnect.
- **A failed commit is rolled back.** If the commit endpoint cannot accept the transaction, the server sends it to the rollback endpoint and the client receives `RolledBack`. A failed timeout delivery is rolled back the same way.

## Server Configuration

### Creating Containers

Create containers after building the rider:

```csharp
HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg => cfg.UseMemoryQueues())
    .Build();

rider.Transaction.CreateOrUpdateContainer("order-payment",
    TimeSpan.FromSeconds(30),
    new QueueTransactionEndpoint(rider.Queue, "payment-commit"),
    new QueueTransactionEndpoint(rider.Queue, "payment-rollback"),
    new QueueTransactionEndpoint(rider.Queue, "payment-timeout"));
```

| Method | Behavior when the container exists |
|---|---|
| `CreateContainer(...)` | Throws `DuplicateNameException` |
| `CreateOrUpdateContainer(...)` | Replaces the definition (timeout, endpoints, handler) and returns the existing container |

Use `CreateOrUpdateContainer` when the containers are defined in code on every application start. A container loaded from `transactions.json` is then updated instead of causing an error.

Parameters:

| Parameter | Required | Description |
|---|---|---|
| `name` | Yes | Container name. Clients begin transactions with this name. |
| `timeout` | Yes | Lifetime of an open transaction. Must be greater than zero and at most `int.MaxValue` milliseconds (about 24.8 days). |
| `commitEndpoint` | Yes | Receives committed transactions. |
| `rollbackEndpoint` | Yes | Receives rolled back transactions, including those the server rolls back. |
| `timeoutEndpoint` | No | Receives timed out transactions. If null, they go to the rollback endpoint. |
| `handler` | No | Custom `IServerTransactionHandler`. If null, `rider.Transaction.DefaultHandler` is used. |

Other container operations:

```csharp
ServerTransactionContainer container = rider.Transaction.Find("order-payment");
ServerTransactionContainer[] all = rider.Transaction.GetContainers();

int open = container.Count;                          // open transactions
ServerTransaction transaction = container.Find(id);  // an open transaction

// rolls back the open transactions of the container, then calls handler.Destroy
bool deleted = await rider.Transaction.DeleteContainer("order-payment");
```

### Persistence

Container definitions are saved into `transactions.json` in the rider's `DataPath` (`data/transactions.json` by default) and loaded when the rider is built.

- Types are saved as `Namespace.TypeName, AssemblyName` (without version), so endpoint and handler types from application assemblies are loaded after a restart and after version upgrades.
- A container that cannot be loaded (for example, its endpoint type is missing) is logged through the rider's error handlers and skipped. The server still starts. The entry stays in the file, so it is loaded on the next start when the problem is fixed, or replaced when the container is defined again.
- An unreadable `transactions.json` is logged and ignored.
- `DefaultHandler` is not saved. A container that uses the default handler is saved without a handler type.

**Open transactions are not saved.** On a graceful shutdown they are rolled back (see [Shutdown](#shutdown)). If the process crashes, open transactions are lost and no endpoint is notified.

### Endpoints

An endpoint receives finished transactions:

```csharp
public interface IServerTransactionEndpoint
{
    string InitParameter { get; }
    Task<bool> Send(ServerTransaction transaction);
}
```

- `Send` returns true when the transaction is delivered. Returning false or throwing an exception is treated as a failed delivery.
- `InitParameter` is saved into `transactions.json` and passed to the constructor when the container is loaded.

#### QueueTransactionEndpoint

`QueueTransactionEndpoint` pushes a message into a queue. The queue does not need to exist when the container is created. It is found, or created with the default queue options, when the first transaction is sent. If the queue is removed and created again later, the endpoint uses the new queue.

The pushed message is a new message built from the begin message:

| Property | Value |
|---|---|
| Message id | The transaction id |
| `Transaction-Id` header | The transaction id |
| Content | The begin message content |
| Headers | Custom headers of the begin message |
| Source | The client that began the transaction |

Headers that change queue behavior (`Queue-Type`, `Queue-Manager`, `Acknowledge`, `Message-Timeout`, `Client-Limit`, `Partition-Limit`, `Partition-Subscribers`, `CC` and similar) are not copied. A client cannot change the queue through its transaction.

`Partition-Label` is copied. If the endpoint queue is partitioned, the message is routed to the partition of the label the client set on the begin message, like a normal push with a label.

#### Custom Endpoints

To be loaded from `transactions.json`, a custom endpoint needs a public constructor whose parameters are any of these types:

| Parameter type | Value |
|---|---|
| `string` | The saved `InitParameter` |
| `HorseRider`, `QueueRider`, `DirectRider`, `ChannelRider`, `RouterRider` | The rider objects |
| `ServerTransactionContainer` | The container being loaded |

The constructor with the most parameters is tried first.

### Handlers

A handler lets the application approve new transactions and observe their outcome:

```csharp
public class PaymentTransactionHandler : IServerTransactionHandler
{
    // false: the transaction is not created, the client receives Unauthorized
    public Task<bool> CanCreate(ServerTransaction transaction) => Task.FromResult(true);

    // called after the transaction is delivered to the endpoint
    public Task Commit(ServerTransaction transaction) => Task.CompletedTask;
    public Task Rollback(ServerTransaction transaction) => Task.CompletedTask;
    public Task Timeout(ServerTransaction transaction) => Task.CompletedTask;

    // called when an endpoint does not accept the transaction
    public Task SendFailed(ServerTransaction transaction, ServerTransactionStatus operation,
        IServerTransactionEndpoint endpoint) => Task.CompletedTask;

    // called after the container is deleted
    public Task Destroy(ServerTransactionContainer container) => Task.CompletedTask;
}
```

- All methods except `CanCreate` are notifications. Exceptions thrown by them are logged and do not change the result of the operation.
- A handler saved into `transactions.json` is created with `Activator.CreateInstance`, so it needs a parameterless constructor. For handlers with dependencies, define the container in code with `CreateOrUpdateContainer` on every start, or set `DefaultHandler`.

### Timeouts

The deadline of a transaction is set when it begins (`now + container timeout`). A single timer per rider checks the deadlines every 100 milliseconds. A timed out transaction can be finished up to about 100 milliseconds after its deadline.

### Shutdown

On process exit and termination signals (SIGTERM, SIGINT), the server shuts down in this order:

1. Queues stop accepting pushes and deliveries. Transaction requests are rejected with `Busy`. Timeout checks stop.
2. Queues are flushed to disk. The stores stay open.
3. Open transactions are rolled back. The server also waits for the commits and rollbacks that are already in progress, whoever started them (client requests, client disconnects, timeouts). Transaction messages are the only pushes queues accept during shutdown. This step waits at most 15 seconds (`TransactionRider.DefaultShutdownTimeout`). Deliveries that do not finish in time are logged and the shutdown continues.
4. Queues are flushed again and closed, so the rollback messages are saved too.

Everything that was in the queues when shutdown started is on disk before any rollback runs. If the process is killed early (for example, the container runtime's stop timeout expires), only rollback messages can be lost. A slow or hanging endpoint cannot block the queue flush for more than 15 seconds.

When the server is stopped by code, call `ShutdownAsync` before stopping it:

```csharp
bool completed = await rider.Transaction.ShutdownAsync();          // waits at most 15 seconds
// or: await rider.Transaction.ShutdownAsync(TimeSpan.FromSeconds(5));
server.Stop();
```

`ShutdownAsync` returns false if the rollbacks did not finish in time. If the server is stopped without `ShutdownAsync`, the clients are disconnected and their open transactions are rolled back by the disconnect rule.

Custom queue managers can implement `IHorseQueueManager.FlushToDisk` to write pending data in step 2 without closing their stores. The default implementation does nothing.

### Clustering

Transactions are not replicated. A transaction is held only by the node its client is connected to. In a reliable cluster, only the main node accepts transaction requests. If the main node fails, its open transactions are lost like on a crash.

## Client-Side API

```csharp
await using HorseTransaction transaction = new HorseTransaction(client, "order-payment");

await transaction.Begin(new PaymentModel { OrderId = 42 }, cancellationToken);

// ... do the work ...

TransactionResult result = await transaction.Commit(cancellationToken);
```

The id is generated by the client's `UniqueIdGenerator`. A user defined id can be given with `new HorseTransaction(client, name, transactionId)`. It must be unique among the open transactions of the container.

### Begin

`Begin` succeeds or throws `TransactionException`. Its `Code` property has the reason:

| Code | Reason |
|---|---|
| `NotFound` | No container with this name on the server |
| `Conflict` | There is already an open transaction with this id |
| `Unauthorized` | The handler's `CanCreate` returned false |
| `Busy` | The server is shutting down |
| `RequestTimeout`, `SendError`, ... | The request did not complete |

`Begin` can be called once per `HorseTransaction`. If the request times out or is cancelled, the client sends a rollback in the background, because the server may have created the transaction.

### Commit and Rollback

`Commit` and `Rollback` return a `TransactionResult`:

| Result | Meaning | Status after |
|---|---|---|
| `Committed` | The transaction is delivered to the commit endpoint | `Commit` |
| `RolledBack` | The transaction is rolled back. `Commit` returns this when the commit could not be delivered. | `Rollback` |
| `NotFound` | The transaction is not open anymore (already finished, timed out, or rolled back after a disconnect) | `Ended` |
| `Unknown` | The request did not complete (no response, send error, server shutting down). The transaction may still be open. | `Begin` |

After `Unknown`, it is safe to retry `Commit` or `Rollback`. The server finishes a transaction once, so a retry either finishes it or receives `NotFound`.

If the connection drops after a commit is sent and before its response arrives, the client cannot learn whether the commit was applied: a later request receives `NotFound` in both cases.

### Disposing

| Method | Behavior |
|---|---|
| `await DisposeAsync()` (`await using`) | Rolls back the transaction if it is still open and waits for the response |
| `Dispose()` (`using`) | Sends the rollback in the background and returns immediately. It never blocks the calling thread. |

Errors while disposing are ignored. If the rollback cannot be sent, the server still rolls the transaction back on timeout or disconnect.

## Protocol

| Item | Value |
|---|---|
| Message type | `MessageType.Transaction` (`0x1B`) |
| Target | Container name |
| Content type | `691` Begin, `692` Commit, `693` Rollback |
| `Transaction-Id` header | Transaction id (required) |
| Message id | Request id. Each request has its own message id and the response uses it. |

The begin message content is the transaction payload. Commit and rollback messages have no content.

Response codes:

| Code | Begin | Commit / Rollback |
|---|---|---|
| `Ok` | Created | Delivered |
| `Failed` | Unexpected server error | Not delivered. The transaction is finished; a failed commit is rolled back. |
| `NotFound` | Unknown container | Transaction is not open or the client is not its owner |
| `Conflict` | Duplicate transaction id | — |
| `Unauthorized` | Rejected by `CanCreate` | — |
| `Busy` | Server is shutting down | Server is shutting down |
| `BadRequest` | Missing `Transaction-Id` header or unknown content type | Same |
