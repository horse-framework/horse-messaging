# Horse Messaging v8.0 Release Notes

**Release Date:** February 2026

This document outlines the changes, new features, and breaking changes between v7.4 and v8.0 of the Horse Messaging library.

---

## Overview

Version 8.0 introduces a major overhaul of the client extensions library, focusing on:
- **C# 13 Extension Members** - Migration from traditional static extension methods to new C# 13 extension member syntax
- **Improved API Design** - More intuitive method naming and simplified configuration
- **Enhanced Graceful Shutdown** - Now includes channel subscription management
- **Better Dependency Injection** - Improved keyed services support and configuration access

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

### 2. Enhanced Configuration Delegate

New configuration overloads that provide access to `IConfiguration`, `IHostEnvironment`, and `IServiceCollection`:

```csharp
// v8.0 - Access to configuration, environment, and services
Host.CreateDefaultBuilder(args)
    .AddHorse((builder, config, env, services) =>
    {
        var connectionString = config.GetConnectionString("Horse");
        
        if (env.IsDevelopment())
        {
            builder.SetReconnectWait(TimeSpan.FromSeconds(2));
        }
        
        services.AddSingleton<MyCustomService>();
    })
    .Build();
```

### 3. Improved Graceful Shutdown Callback

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

The `HorseRunnerHostedService` class has been removed. Auto-connection is now handled internally by `HorseServiceProviderFactory`.

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

---

## 📦 Files Changed

| File | Change Type |
|------|-------------|
| `Horse.Messaging.Client/Channels/ChannelOperator.cs` | Modified - Added `ActiveChannelOperations` and `UnsubscribeFromAllChannels()` |
| `Horse.Messaging.Client/HorseClientBuilder.cs` | Modified - Removed `AddServices()`, updated shutdown logic |
| `Horse.Messaging.Client/HorseClientExtensions.cs` | **Deleted** - Moved to Extensions.Client |
| `Horse.Messaging.Extensions.Client/ClientBuilderExtensions.cs` | Modified - Renamed methods, uses C# 13 extension syntax |
| `Horse.Messaging.Extensions.Client/GenericHostExtensions.cs` | Modified - Complete rewrite with C# 13 extension syntax |
| `Horse.Messaging.Extensions.Client/GracefulShutdownService.cs` | Modified - Added channel support, updated callback signature |
| `Horse.Messaging.Extensions.Client/HorseClientExtensions.cs` | **New** - Moved from Client library |
| `Horse.Messaging.Extensions.Client/HorseRunnerHostedService.cs` | **Deleted** |
| `Horse.Messaging.Extensions.Client/HorseServiceProviderFactory.cs` | Modified - Enhanced configuration support |
| `Horse.Messaging.Extensions.Client/README.md` | **New** - Comprehensive documentation |

---

## 💡 Quick Reference

### Complete v8.0 Example

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;

var builder = Host.CreateDefaultBuilder(args);

// Configure Horse Messaging with all new features
builder.AddHorse((horseBuilder, config, env, services) =>
{
    // Get connection string from configuration
    var connectionString = config.GetConnectionString("Horse") 
                           ?? "horse://localhost:26200";
    
    horseBuilder.AddHost(connectionString)
                .SetClientName("MyService")
                .SetClientType("Consumer")
                .AddTransientConsumers(typeof(Program));
    
    // Use graceful shutdown with IServiceProvider access
    horseBuilder.UseGracefulShutdown(
        minWait: TimeSpan.FromSeconds(2),
        maxWait: TimeSpan.FromSeconds(30),
        shuttingDownAction: async (serviceProvider) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Graceful shutdown initiated...");
        }
    );
});

var host = builder.Build();
await host.RunAsync();
```

### Keyed Services Example

```csharp
var builder = Host.CreateDefaultBuilder(args);

// Primary connection
builder.AddHorse("primary", primaryBuilder =>
{
    primaryBuilder.AddHost("horse://primary-server:26200");
});

// Secondary connection
builder.AddHorse("secondary", secondaryBuilder =>
{
    secondaryBuilder.AddHost("horse://secondary-server:26200");
});

var host = builder.Build();
await host.RunAsync();

// Usage in services
public class MyService
{
    public MyService(
        [FromKeyedServices("primary")] HorseClient primaryClient,
        [FromKeyedServices("secondary")] HorseClient secondaryClient)
    {
        // Use both clients
    }
}
```

---

## 🔧 Requirements

- .NET 9.0 or later (for C# 13 extension members support)
- Microsoft.Extensions.Hosting 9.0.0 or later

---

## 📚 Additional Resources

- [Horse Messaging Documentation](https://github.com/horse-framework/horse-messaging)
- [Horse.Messaging.Extensions.Client README](src/Horse.Messaging.Extensions.Client/README.md)

