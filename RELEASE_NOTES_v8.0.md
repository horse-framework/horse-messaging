# Horse Messaging v8.0 Release Notes

**Release Date:** February 2026

This document outlines the changes, new features, and breaking changes between v7.4 and v8.0 of the Horse Messaging library.

---

## Overview

Version 8.0 introduces a major overhaul of the client extensions library, focusing on:
- **C# 13 Extension Members** - Migration from traditional static extension methods to new C# 13 extension member syntax
- **Improved API Design** - More intuitive method naming and simplified configuration
- **Enhanced Graceful Shutdown** - Now includes channel subscription management
- **Better Dependency Injection** - Full Add/Use convention; typed, keyed and combined overloads; `autoConnect` parameter; `HorseConnectService` hosted service
- **Complete Configure Delegate Matrix** - All 8 combinations of `IConfiguration`, `IHostEnvironment` and `IServiceCollection` parameters across every host target
- **Queue Partition System** - Automatic sub-queue partitioning for tenant isolation and lock-free scaling

---

## 🚀 New Features

### 1. Channel Graceful Shutdown Support

Graceful shutdown now supports channel operations in addition to queue operations:

```csharp
// New: Channels are now automatically unsubscribed during graceful shutdown
builder.UseGracefulShutdown(
    minWait: TimeSpan.FromSeconds(1),
    maxWait: TimeSpan.FromSeconds(30)
);
```

**New Property in `ChannelOperator`:**
```csharp
// Track active channel operations
public int ActiveChannelOperations { get; }
```

**New Method in `ChannelOperator`:**
```csharp
// Unsubscribe from all channels at once
public Task<HorseResult> UnsubscribeFromAllChannels();
```

### 2. Complete Configure Delegate Overload Matrix

Every `AddHorse` / `AddKeyedHorse` overload is now available on all three host targets
(`IServiceCollection`, `IHostBuilder`, `IHostApplicationBuilder`) for all **8 parameter combinations**
of `IConfiguration`, `IHostEnvironment` and `IServiceCollection`.

| Configure signature | `IServiceCollection` | `IHostBuilder` | `IHostApplicationBuilder` |
|---|:---:|:---:|:---:|
| `(builder)` | ✅ | ✅ | ✅ |
| `(builder, cfg)` | ✅ | ✅ | ✅ |
| `(builder, env)` | ✅ | ✅ | ✅ |
| `(builder, svc)` | ✅ | ✅ | ✅ |
| `(builder, cfg, env)` | ✅ | ✅ | ✅ |
| `(builder, cfg, svc)` | ✅ | ✅ | ✅ |
| `(builder, env, svc)` | ✅ | ✅ | ✅ |
| `(builder, cfg, env, svc)` | ✅ | ✅ | ✅ |

Each row also has a **keyed** variant (`AddKeyedHorse` / `AddHorse(key, ...)`),
giving **48 overloads** in total (16 signatures × 3 targets).

```csharp
// Only IConfiguration needed
builder.AddHorse((horseBuilder, config) =>
{
    horseBuilder.AddHost(config["Horse:Host"]);
});

// Only IServiceCollection needed (register additional services alongside Horse)
builder.AddHorse((horseBuilder, services) =>
{
    services.AddSingleton<IMyDependency, MyDependency>();
    horseBuilder.AddHost("horse://localhost:26200");
});

// IConfiguration + IHostEnvironment
builder.AddHorse((horseBuilder, config, env) =>
{
    var host = env.IsDevelopment() ? "horse://localhost:26200" : config["Horse:Host"];
    horseBuilder.AddHost(host);
});

// All four (existing behaviour, now consistent with the others)
builder.AddHorse((horseBuilder, config, env, services) =>
{
    services.AddSingleton<IMyDependency, MyDependency>();
    horseBuilder.AddHost(config["Horse:Host"]);
});
```

### 3. DI Extension API Redesign — Proper Add / Use Convention

The extension layer has been fully rewritten to follow the standard .NET `Add*` / `Use*` pattern
and to remove the `HorseServiceProviderFactory` dependency from `IHostApplicationBuilder`.

#### `autoConnect` parameter

All `AddHorse` overloads now accept an `autoConnect` flag (default `true`).
When `false`, the client does **not** connect on host start — call `host.UseHorse()` manually.

```csharp
// Manual connect (e.g. after warm-up tasks)
builder.AddHorse(b => b.AddHost("horse://localhost:26200"), autoConnect: false);
var host = builder.Build();
// ... warm-up ...
host.UseHorse();   // ← connects here
```

#### New typed `UseHorse<TIdentifier>` on `IHost` and `IServiceProvider`

```csharp
// Typed connection
host.UseHorse<PrimaryConnection>();
host.UseHorse<PrimaryConnection>("key");  // keyed + typed

provider.UseHorse<PrimaryConnection>();
provider.UseHorse<PrimaryConnection>("key");
```

#### `HorseConnectService` — replaces internal `HorseServiceProviderFactory` auto-connect

When `autoConnect = true`, a lightweight `IHostedService` (`HorseConnectService`) is registered.
It calls `client.Connect()` in `StartAsync`, which integrates correctly with the .NET host
lifecycle (after all services are built, before the application starts processing).

Previously `IHostApplicationBuilder.ConfigureContainer` was used — this was silently ignored
because `IHostApplicationBuilder` does not support custom `IServiceProviderFactory`.
**The connection was never established reliably with `WebApplication.CreateBuilder`.**
This bug is now fixed.

#### New files

| File | Purpose |
|---|---|
| `HorseConnectService.cs` | `IHostedService` that calls `client.Connect()` on `StartAsync` |
| `HorseRegistrar.cs` | Internal static helper — core registration logic, avoids C# 13 extension member self-call resolution issues |

### 4. Improved Graceful Shutdown Callback

The shutdown callback now receives `IServiceProvider` for better service access:

```csharp
// v8.0 - IServiceProvider is now available in callback
builder.UseGracefulShutdown(
    minWait: TimeSpan.FromSeconds(2),
    maxWait: TimeSpan.FromSeconds(30),
    shuttingDownAction: async (serviceProvider) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Shutting down...");
    }
);
```

### 5. Queue Partition System

Queues can now be automatically split into physical sub-queues (partitions). From the outside it still looks like a single queue — routing is handled internally by `PartitionManager`.

**Key capabilities:**

| Feature | Description |
|---|---|
| **Tenant isolation** | Label-based routing keeps each tenant's messages in a dedicated partition |
| **Lock-free scaling** | Each worker owns its partition — zero contention |
| **Dynamic expansion** | New worker → new partition; worker drops → partition auto-destroyed |
| **Auto-assign workers** | `AutoAssignWorkers = true` — label-less workers are pooled and assigned to labeled partitions on demand. `MaxPartitionsPerWorker` controls how many partitions a single worker serves simultaneously (0 = unlimited). |
| **Per-partition AutoDestroy** | `NoConsumers` / `NoMessages` / `Empty` — only the affected partition is destroyed |
| **Metrics** | Partition count, message count, consumer count per partition via `QueueInfo` |
| **Events** | `IPartitionEventHandler.OnPartitionCreated/OnPartitionDestroyed` on server |

**Server-side setup:**
```csharp
rider.Queue.Options.AutoQueueCreation = true; // partition queues are auto-created
```

**Client subscribe:**
```csharp
// Dedicated partition for a label (tenant isolation)
await client.Queue.SubscribePartitioned(
    queue:                   "FetchOrders",
    partitionLabel:          "tenant-42",
    verifyResponse:          true,
    maxPartitions:           10,
    subscribersPerPartition: 1);

// Unlimited partitions (overrides server default)
await client.Queue.SubscribePartitioned("FetchOrders", "tenant-42", true, maxPartitions: 0);

// Label-less (round-robin distribution across partitions)
await client.Queue.SubscribePartitioned("JobQueue", null, true, maxPartitions: 5);

// Server default (maxPartitions not sent)
await client.Queue.SubscribePartitioned("JobQueue", "label", true);
```

**Attribute-based (auto-subscribe on connect):**
```csharp
// Explicit limit: 10 partitions, 1 subscriber each
[PartitionedQueue("tenant-42", MaxPartitions = 10, SubscribersPerPartition = 1)]
public class FetchOrderConsumer : IQueueConsumer<FetchOrderEvent> { ... }

// Unlimited partitions (override server default)
[PartitionedQueue("tenant-42", MaxPartitions = 0)]
public class UnlimitedConsumer : IQueueConsumer<FetchOrderEvent> { ... }
```

> **Attribute defaults:** `MaxPartitions` and `SubscribersPerPartition` default to `-1` (not set — server default used). `0` = unlimited. `> 0` = explicit limit. The API parameters (`SubscribePartitioned`, builder overloads) use `int?` where `null` = not sent (server default).

**Produce (unchanged — producer writes to the parent queue name):**
```csharp
// Without label → round-robin across partitions
await client.Queue.Push(message, false);

// With label → dedicated partition
await client.Queue.Push("FetchOrders", message, false,
    new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-42") });
```

> Full details: [`docs/queues/partitioned-queues.md`](docs/queues/partitioned-queues.md)

---

## ⚠️ Breaking Changes

### 1. Extension Methods Renamed and Restructured

The extension methods have been completely restructured using C# 13 extension members syntax. This requires code changes when upgrading.

#### IHostBuilder Extensions

| v7.4 Method | v8.0 Method | Notes |
|-------------|-------------|-------|
| `UseHorse(cfg)` | `AddHorse(cfg)` | Method renamed |
| `UseHorse(cfg, autoConnect)` | `AddHorse(cfg, autoConnect)` | Method renamed |
| `ConfigureHorseClient(cfg)` | **REMOVED** | Use `AddHorse` directly |

**v7.4 Usage:**
```csharp
Host.CreateDefaultBuilder(args)
    .UseHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200");
    })
    .Build();
```

**v8.0 Usage:**
```csharp
Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200");
    })
    .Build();
```

### 2. IServiceCollection Extensions Renamed

| v7.4 Method | v8.0 Method | Notes |
|-------------|-------------|-------|
| `AddHorseBus(cfg)` | `AddHorse(cfg)` | Renamed |
| `AddHorseBus<TIdentifier>(cfg)` | `AddHorse<TIdentifier>(cfg)` | Renamed |
| `AddKeyedHorseBus(key, cfg)` | `AddKeyedHorse(key, cfg)` | Renamed |

**v7.4 Usage:**
```csharp
services.AddHorseBus(builder =>
{
    builder.AddHost("horse://localhost:26200");
});
```

**v8.0 Usage:**
```csharp
services.AddHorse(builder =>
{
    builder.AddHost("horse://localhost:26200");
});
```

### 3. IServiceProvider Extensions Renamed

| v7.4 Method | v8.0 Method | Notes |
|-------------|-------------|-------|
| `UseHorseBus()` | `UseHorse()` | Renamed |
| `UseKeyedHorseBus(key)` | `UseHorse(key)` | Renamed, now overloaded |

**v7.4 Usage:**
```csharp
app.Services.UseHorseBus();
app.Services.UseKeyedHorseBus("primary");
```

**v8.0 Usage:**
```csharp
app.Services.UseHorse();
app.Services.UseHorse("primary");
```

### 4. HorseClientBuilder Extensions Renamed

| v7.4 Method | v8.0 Method | Notes |
|-------------|-------------|-------|
| `UseGracefulShutdownHostedService(min, max)` | `UseGracefulShutdown(min, max)` | Renamed, moved to HorseClientBuilder |
| `UseGracefulShutdownHostedService(min, max, action)` | `UseGracefulShutdown(min, max, action)` | Renamed, action signature changed |

**v7.4 Usage:**
```csharp
builder.UseGracefulShutdownHostedService(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30)
);

// With callback (v7.4 - Func<Task>)
builder.UseGracefulShutdownHostedService(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30),
    async () => { /* shutdown logic */ }
);
```

**v8.0 Usage:**
```csharp
builder.UseGracefulShutdown(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30)
);

// With callback (v8.0 - Func<IServiceProvider, Task> or Func<Task>)
builder.UseGracefulShutdown(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30),
    async (serviceProvider) => { /* shutdown logic with DI access */ }
);

// Or without IServiceProvider
builder.UseGracefulShutdown(
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(30),
    async () => { /* shutdown logic */ }
);
```

### 5. Extension Method Location Changed

The extension methods have been moved from `Horse.Messaging.Client` to `Horse.Messaging.Extensions.Client`:

| v7.4 Location | v8.0 Location |
|---------------|---------------|
| `Horse.Messaging.Client.HorseClientExtensions` | `Horse.Messaging.Extensions.Client.HorseClientExtensions` |

**Update your using statements:**
```csharp
// v7.4
using Horse.Messaging.Client;

// v8.0
using Horse.Messaging.Extensions.Client;
```

### 6. HorseClientBuilder.AddServices() Removed

The `AddServices()` method has been removed from `HorseClientBuilder`. Services are now automatically configured through the constructor.

**v7.4 Usage:**
```csharp
services.AddHorseBus(b =>
{
    b.AddServices(services); // Required in v7.4
    b.AddHost("horse://localhost:26200");
});
```

**v8.0 Usage:**
```csharp
services.AddHorse(b =>
{
    // AddServices() no longer needed
    b.AddHost("horse://localhost:26200");
});
```

### 7. HorseRunnerHostedService Removed

`HorseRunnerHostedService` has been removed. Auto-connection is now handled by the new
`HorseConnectService` (registered automatically when `autoConnect = true`).
The previous implementation relied on `HorseServiceProviderFactory.CreateServiceProvider`,
which did not work correctly with `IHostApplicationBuilder` (e.g. `WebApplication.CreateBuilder`).
`HorseConnectService` runs in the standard `IHostedService` pipeline and is fully lifecycle-aware.

### 8. Json Suffix Removed from All Bus APIs

All `*Json` suffixed methods have been replaced with clean generic overloads across every bus interface. The `object` parameter is now `T model` with `where T : class` constraint.

**Queue Bus (`IHorseQueueBus` / `QueueOperator`):**

| v7.4 Method | v8.0 Method |
|---|---|
| `PushJson(object, ...)` | `Push<T>(T model, ...)` |
| `PushJson(string queue, object, ...)` | `Push<T>(string queue, T model, ...)` |
| `PushBulkJson<T>(...)` | `PushBulk<T>(...)` |

**Router Bus (`IHorseRouterBus` / `RouterOperator`):**

| v7.4 Method | v8.0 Method |
|---|---|
| `PublishJson(object, ...)` | `Publish<T>(T model, ...)` |
| `PublishJson(string router, object, ...)` | `Publish<T>(string router, T model, ...)` |
| `PublishRequestJson<TReq, TResp>(...)` | `PublishRequest<TReq, TResp>(...)` |

**Direct Bus (`IHorseDirectBus` / `DirectOperator`):**

| v7.4 Method | v8.0 Method |
|---|---|
| `SendJson(object, ...)` | `Send<T>(T model, ...)` |
| `SendJsonByName<T>(...)` | `SendByName<T>(...)` |
| `SendJsonByType<T>(...)` | `SendByType<T>(...)` |
| `SendJsonById<T>(...)` | `SendById<T>(...)` |
| `SendDirectJsonAsync<T>(...)` | `SendAsync<T>(...)` |
| `RequestJsonAsync<TResp>(...)` | `Request<TResp>(...)` |
| `RequestJsonAsync<TReq, TResp>(...)` | `Request<TReq, TResp>(...)` |

**HorseClient:**

| v7.4 Method | v8.0 Method |
|---|---|
| `SendAndGetJson<T>(...)` | `SendAndGet<T>(...)` |

---

## 📝 Migration Guide

### Step 1: Update Package References

Ensure you're using the latest version of the package:
```xml
<PackageReference Include="Horse.Messaging.Extensions.Client" Version="8.0.0" />
```

### Step 2: Update Using Statements

```csharp
// Remove or update
using Horse.Messaging.Client; // If only using extension methods

// Add
using Horse.Messaging.Extensions.Client;
```

### Step 3: Rename Extension Method Calls

Use find-and-replace to update method calls:

| Find | Replace |
|------|---------|
| `.UseHorse(` | `.AddHorse(` |
| `.AddHorseBus(` | `.AddHorse(` |
| `.AddKeyedHorseBus(` | `.AddKeyedHorse(` |
| `.UseHorseBus()` | `.UseHorse()` |
| `.UseKeyedHorseBus(` | `.UseHorse(` |
| `.UseGracefulShutdownHostedService(` | `.UseGracefulShutdown(` |
| `.PushJson(` | `.Push<T>(` |
| `.PushBulkJson(` | `.PushBulk<T>(` |
| `.PublishJson(` | `.Publish<T>(` |
| `.PublishRequestJson<` | `.PublishRequest<` |
| `.SendJson(` | `.Send<T>(` |
| `.SendJsonByName(` | `.SendByName(` |
| `.SendJsonByType(` | `.SendByType(` |
| `.SendJsonById(` | `.SendById(` |
| `.SendDirectJsonAsync(` | `.SendAsync<T>(` |
| `.RequestJsonAsync<` | `.Request<` |
| `.SendAndGetJson<` | `.SendAndGet<` |

### Step 4: Remove AddServices() Calls

Remove any `.AddServices(services)` calls from your configuration:

```csharp
// Before
services.AddHorseBus(b =>
{
    b.AddServices(services); // ❌ Remove this line
    b.AddHost("horse://localhost:26200");
});

// After
services.AddHorse(b =>
{
    b.AddHost("horse://localhost:26200");
});
```

### Step 5: Update Graceful Shutdown Callbacks

If you're using the graceful shutdown callback, update the signature:

```csharp
// Before (v7.4)
.UseGracefulShutdownHostedService(min, max, async () =>
{
    // No access to services
});

// After (v8.0) - with IServiceProvider
.UseGracefulShutdown(min, max, async (serviceProvider) =>
{
    var myService = serviceProvider.GetRequiredService<IMyService>();
    await myService.PrepareForShutdownAsync();
});

// Or without IServiceProvider
.UseGracefulShutdown(min, max, async () =>
{
    // shutdown logic without service access
});
```

### Step 6: Remove ConfigureHorseClient Calls

If you were using `ConfigureHorseClient`, replace it with `AddHorse`:

```csharp
// Before (v7.4)
Host.CreateDefaultBuilder(args)
    .ConfigureHorseClient(builder => { /* config */ })
    .UseServiceProviderFactory(context => new HorseServiceProviderFactory(context))
    .Build();

// After (v8.0)
Host.CreateDefaultBuilder(args)
    .AddHorse(builder => { /* config */ })
    .Build();
```

### Step 7: Fix `WebApplication.CreateBuilder` Auto-Connect

If you used `AddHorse` with `WebApplication.CreateBuilder` (ASP.NET Core minimal API) and the
client was **not connecting**, this was a known bug caused by `ConfigureContainer` being silently
ignored on `IHostApplicationBuilder`. Upgrade to v8.0 — the bug is fixed automatically
via `HorseConnectService`.

```csharp
// Before (v7.4 — connection might silently fail on IHostApplicationBuilder)
var builder = WebApplication.CreateBuilder(args);
builder.AddHorse(b => b.AddHost("horse://localhost:26200"));

// After (v8.0 — works correctly, HorseConnectService handles the connect)
var builder = WebApplication.CreateBuilder(args);
builder.AddHorse(b => b.AddHost("horse://localhost:26200")); // ← identical, now reliable
```

---

## 📦 Files Changed

| File | Change Type |
|------|-------------|
| `Horse.Messaging.Client/Channels/ChannelOperator.cs` | Modified — Added `ActiveChannelOperations` and `UnsubscribeFromAllChannels()` |
| `Horse.Messaging.Client/HorseClientBuilder.cs` | Modified — Removed `AddServices()`, updated shutdown logic |
| `Horse.Messaging.Client/HorseClientExtensions.cs` | **Deleted** — Moved to Extensions.Client |
| `Horse.Messaging.Extensions.Client/HorseClientExtensions.cs` | **New** — `IServiceCollection` Add/Use extensions (all 8 delegate combinations + keyed/typed variants) |
| `Horse.Messaging.Extensions.Client/GenericHostExtensions.cs` | **Rewritten** — `IHostBuilder` / `IHostApplicationBuilder` / `IHost` with full overload matrix; uses `ConfigureServices` instead of `UseServiceProviderFactory` |
| `Horse.Messaging.Extensions.Client/HorseRegistrar.cs` | **New** — Internal static registration helper (avoids C# 13 extension member self-call issues) |
| `Horse.Messaging.Extensions.Client/HorseConnectService.cs` | **New** — `IHostedService` that connects the client on host start; replaces `HorseRunnerHostedService` |
| `Horse.Messaging.Extensions.Client/GracefulShutdownService.cs` | Modified — Added channel unsubscribe support, `IServiceProvider` callback |
| `Horse.Messaging.Extensions.Client/HorseServiceProviderFactory.cs` | Kept for backwards compatibility — no longer used by `GenericHostExtensions` |
| `Horse.Messaging.Extensions.Client/ClientBuilderExtensions.cs` | Modified — Renamed methods, C# 13 extension syntax |
| `Horse.Messaging.Extensions.Client/HorseRunnerHostedService.cs` | **Deleted** |
| `Horse.Messaging.Extensions.Client/README.md` | **New** — Comprehensive documentation |
| `Horse.Messaging.Server/Queues/Partitions/` | **New** — PartitionManager, PartitionEntry, IPartitionEventHandler and supporting types |
| `Horse.Messaging.Client/Queues/Annotations/PartitionedQueueAttribute.cs` | **New** — Single attribute for partition configuration |
| `Horse.Messaging.Client/Queues/QueueOperator.cs` | Modified — `SubscribePartitioned`, `IQueueBus` partition overloads |
| `Horse.Messaging.Client/HorseClient.cs` | Modified — Auto-subscribe partition awareness, sub-queue routing |

---

## 💡 Quick Reference

### Minimal Setup

```csharp
// IHostBuilder (Host.CreateDefaultBuilder)
Host.CreateDefaultBuilder(args)
    .AddHorse(b => b.AddHost("horse://localhost:26200"))
    .Build();

// IHostApplicationBuilder (WebApplication.CreateBuilder / Host.CreateApplicationBuilder)
var builder = WebApplication.CreateBuilder(args);
builder.AddHorse(b => b.AddHost("horse://localhost:26200"));
var app = builder.Build();

// IServiceCollection (manual)
services.AddHorse(b => b.AddHost("horse://localhost:26200"));
```

### Partial Configure Delegates

```csharp
// Only IConfiguration
builder.AddHorse((horseBuilder, config) =>
    horseBuilder.AddHost(config["Horse:Host"]));

// IConfiguration + IHostEnvironment
builder.AddHorse((horseBuilder, config, env) =>
{
    var host = env.IsDevelopment() ? "horse://localhost:26200" : config["Horse:Host"];
    horseBuilder.AddHost(host);
});

// IServiceCollection only — register extra services alongside Horse
builder.AddHorse((horseBuilder, services) =>
{
    services.AddSingleton<IMyService, MyService>();
    horseBuilder.AddHost("horse://localhost:26200");
});

// Full four-parameter variant
builder.AddHorse((horseBuilder, config, env, services) =>
{
    services.AddSingleton<IMyService, MyService>();
    horseBuilder.AddHost(config["Horse:Host"]);
});
```

### Manual Connect (`autoConnect = false`)

```csharp
builder.AddHorse(b => b.AddHost("horse://localhost:26200"), autoConnect: false);
var host = builder.Build();
// ... warm-up, migrations, etc. ...
host.UseHorse();   // connect here
```

### Typed Connections (Multiple Horse Instances)

```csharp
// Registration
services.AddHorse<PrimaryBus>(b => b.AddHost("horse://primary:26200"));
services.AddHorse<SecondaryBus>(b => b.AddHost("horse://secondary:26200"));

// Manual connect
provider.UseHorse<PrimaryBus>();
provider.UseHorse<SecondaryBus>();

// Injection
public class MyService(HorseClient<PrimaryBus> primary, HorseClient<SecondaryBus> secondary) { }
```

### Keyed Connections

```csharp
// Registration
builder.AddHorse("primary",   b => b.AddHost("horse://primary:26200"));
builder.AddHorse("secondary", b => b.AddHost("horse://secondary:26200"));

// Manual connect (when autoConnect = false)
host.UseHorse("primary");
host.UseHorse("secondary");

// Injection
public class MyService(
    [FromKeyedServices("primary")]   HorseClient primaryClient,
    [FromKeyedServices("secondary")] HorseClient secondaryClient) { }
```

### Graceful Shutdown

```csharp
builder.AddHorse(b =>
{
    b.AddHost("horse://localhost:26200")
     .UseGracefulShutdown(
         minWait: TimeSpan.FromSeconds(2),
         maxWait: TimeSpan.FromSeconds(30),
         shuttingDownAction: async (serviceProvider) =>
         {
             var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
             logger.LogInformation("Graceful shutdown initiated...");
         });
});
```

---

## ✅ CancellationToken Support in All Consumer / Handler Interfaces

All consumer, subscriber and handler interfaces now accept an optional `CancellationToken` parameter.  
The token is **automatically cancelled** when the client disconnects or graceful shutdown begins.

### Changed Interfaces

| Interface | Changed Method |
|---|---|
| `IQueueConsumer<TModel>` | `Consume(..., CancellationToken ct = default)` |
| `IChannelSubscriber<TModel>` | `Handle(..., CancellationToken ct = default)` · `Error(..., CancellationToken ct = default)` |
| `IDirectMessageHandler<TModel>` | `Handle(..., CancellationToken ct = default)` |
| `IHorseRequestHandler<TReq,TResp>` | `Handle(..., CancellationToken ct = default)` · `OnError(..., CancellationToken ct = default)` |
| `IHorseInterceptor` | `Intercept(..., CancellationToken ct = default)` |

### New HorseClient property

```csharp
/// <summary>Cancelled when the client disconnects or shuts down gracefully.</summary>
public CancellationToken ConsumeToken { get; }
```

### Usage — Pass the token to your I/O calls

```csharp
public class OrderConsumer : IQueueConsumer<OrderEvent>
{
    private readonly HttpClient _http;
    private readonly IOrderRepository _repo;

    public async Task Consume(HorseMessage message, OrderEvent model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        await _http.PostAsJsonAsync("/api/orders", model, cancellationToken);
        await _repo.SaveAsync(model, cancellationToken);
    }
}
```

### Graceful Shutdown behaviour (improved)

```
SIGTERM
  → _consumeCts.Cancel()       ← new: signals all active consumers
  → active consumers receive OperationCanceledException
  → HttpClient / EF Core / Task.Delay calls stop immediately
  → ActiveConsumeOperations → 0
  → Disconnect()
```

**Backward compatible:** All signatures use `default` parameter — existing implementations continue to compile without any changes.

### Test Coverage

16 unit tests added under `Tests/Test.Client/CancellationTokenTests/`:

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `ConsumeToken_IsNotCancelled_OnCreation` | Fresh `HorseClient` has a live, cancellable token (not `CancellationToken.None`) |
| 2 | `ConsumeToken_IsNotCancelled_AfterConnect` | Token stays live after successful `ConnectAsync` |
| 3 | `ConsumeToken_IsCancelled_AfterDisconnect` | `Disconnect()` cancels the captured token |
| 4 | `ConsumeToken_IsRefreshed_AfterReconnect` | Reconnect produces a new, non-cancelled token |
| 5 | `MultipleDisconnects_DoNotThrow` | Double/triple `Disconnect()` calls are idempotent |
| 6 | `QueueConsumer_ReceivesLiveConsumeToken` | Consumer's `cancellationToken` param equals `client.ConsumeToken` |
| 7 | `QueueConsumer_BlockedOnToken_IsInterruptedOnDisconnect` | Long-running consumer unblocks immediately on `Disconnect()` |
| 8 | `CancellableDelay_StopsEarlyOnDisconnect` | `Task.Delay(token)` stops in <5 s when client disconnects |
| 9 | `TokenIsolation_DisconnectOneClient_OtherUnaffected` | Disconnecting client-1 does **not** cancel client-2's token |
| 10 | `MultipleMessages_AllReceiveSameLiveToken` | All 5 messages see the same live token instance |
| 11 | `MultipleReconnects_EachCycleProducesUniqueToken` | 3 connect-disconnect cycles each yield a distinct token |
| 12 | `GracefulShutdown_CancelsConsumeToken` | `UseGracefulShutdown` → `Disconnect()` cancels token |
| 13 | `ReconnectAfterCancel_DoesNotThrow_AndProducesFreshToken` | No exception; old token stays cancelled, new token is live |
| 14 | `DirectHandler_ReceivesLiveConsumeToken` | `IDirectMessageHandler.Handle` receives live token |
| 15 | `DirectHandler_Blocked_IsInterruptedOnDisconnect` | Blocking `DirectHandler` is interrupted on `Disconnect()` |
| 16 | `ChannelSubscriber_ReceivesLiveConsumeToken` | `IChannelSubscriber.Handle` receives live token |

---

## ⚠️ Breaking Changes — API Cleanup

### String Content Overloads Removed

The `string content` overloads on `QueueOperator.Push` and `RouterOperator.Publish` have been **removed** to eliminate ambiguity with the `Push<T>` / `Publish<T>` generic methods (where `T=string` caused `CS0121` compile errors).

**Before (v7.x):**
```csharp
await client.Queue.Push("my-queue", "Hello", false);           // string overload — REMOVED
await client.Router.Publish("router", "Hello", true);          // string overload — REMOVED
```

**After (v8.0):**
```csharp
await client.Queue.Push("my-queue", Encoding.UTF8.GetBytes("Hello"), false);       // byte[] overload
await client.Queue.Push("my-queue", new MemoryStream(data), false);                // MemoryStream overload
await client.Router.Publish("router", Encoding.UTF8.GetBytes("Hello"), messageId: null, waitForAcknowledge: true);
```

### RouterOperator.Publish — Convenience byte[] Overload Merged

The two `byte[]` `Publish` overloads (with/without `messageId`) have been merged into a single overload with `string messageId = null` as a default parameter:

```csharp
// Single overload — pass null or omit messageId
Task<HorseResult> Publish(string routerName, byte[] data,
    string messageId = null, bool waitForAcknowledge = false, ...);
```

### QueueOperator.Push — byte[] Convenience Overloads Added

New `byte[]` overloads added to `QueueOperator` for simpler raw data pushing:

```csharp
Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, ...);
Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit, ...);
```

### Parameter Order Consistency

All public APIs now follow a consistent parameter order:
`(target, content/model, [messageId], waitForCommit/waitForAcknowledge, [contentType], messageHeaders, [partitionLabel], cancellationToken)`

**After (v8.0):**
```csharp
await bus.Push("FetchOrders", model, false, partitionLabel: "tenant-42");
await bus.Push("FetchOrders", stream, false, partitionLabel: "tenant-42");
// Without partition — exactly the same as before
await bus.Push("FetchOrders", model, false);
```

### PushBulk Methods Added to IHorseQueueBus

`PushBulk<T>` (model) and `PushBulk` (raw `MemoryStream`) are now exposed on `IHorseQueueBus`:

```csharp
// Bulk push — typed models
bus.PushBulk<OrderEvent>("order-queue", orders, (msg, ok) => Console.WriteLine(ok));

// Bulk push — raw binary
bus.PushBulk("order-queue", streams, waitForCommit: true, callback: (msg, ok) => { });
```

---

## 🔧 Requirements

- .NET 10.0 or later (for C# 13 extension members support)
- Microsoft.Extensions.Hosting 10.0.0 or later

---

## 📚 Additional Resources

- [Horse Messaging Documentation](https://github.com/horse-framework/horse-messaging)
- [Horse.Messaging.Extensions.Client README](src/Horse.Messaging.Extensions.Client/README.md)

