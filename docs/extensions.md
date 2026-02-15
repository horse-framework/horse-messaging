# Horse.Messaging.Extensions.Client

Horse Messaging .NET Generic Host (Microsoft.Extensions.Hosting) integration library. This library allows you to easily integrate Horse Messaging Client into your .NET Generic Host applications.

## Installation

```bash
dotnet add package Horse.Messaging.Extensions.Client
```

## Features

- ✅ IHostBuilder and IHostApplicationBuilder support
- ✅ Automatic connection management
- ✅ Keyed Services suppor~~~~t
- ✅ Graceful Shutdown support
- ✅ Dependency Injection integration
- ✅ Configuration and IHostEnvironment access

---

## Extension Methods

### 1. IServiceCollection Extensions

#### AddHorse

Used for basic Horse Client configuration.

**Usage:**

```csharp
services.AddHorse(builder =>
{
    builder.AddHost("horse://localhost:26200")
           .SetClientName("MyClient")
           .SetClientType("Consumer");
});
```

**Parameters:**
- `config`: HorseClientBuilder configuration delegate

**Registered Services:**
- `HorseClient` - Main Horse Client instance
- `IHorseCache` - For cache operations
- `IHorseChannelBus` - For channel operations
- `IHorseQueueBus` - For queue operations
- `IHorseRouterBus` - For router operations
- `IHorseDirectBus` - For direct messaging operations

#### AddHorse\<TIdentifier\>

Horse Client configuration with generic identifier.

**Usage:**

```csharp
services.AddHorse<string>(builder =>
{
    builder.AddHost("horse://localhost:26200")
           .SetClientName("MyClient");
});
```

**Registered Services:**
- `HorseClient<TIdentifier>`
- `IHorseCache<TIdentifier>`
- `IHorseChannelBus<TIdentifier>`
- `IHorseQueueBus<TIdentifier>`
- `IHorseRouterBus<TIdentifier>`
- `IHorseDirectBus<TIdentifier>`

#### AddKeyedHorse

Horse Client configuration with Keyed Services support.

**Usage:**

```csharp
services.AddKeyedHorse("primary", builder =>
{
    builder.AddHost("horse://primary-server:26200");
});

services.AddKeyedHorse("secondary", builder =>
{
    builder.AddHost("horse://secondary-server:26200");
});
```

**Usage (in Controller/Service):**

```csharp
public class MyService
{
    private readonly HorseClient _primaryClient;
    private readonly HorseClient _secondaryClient;

    public MyService(
        [FromKeyedServices("primary")] HorseClient primaryClient,
        [FromKeyedServices("secondary")] HorseClient secondaryClient)
    {
        _primaryClient = primaryClient;
        _secondaryClient = secondaryClient;
    }
}
```

#### UseHorse

Starts the Horse Client and connects to the server. Must be called manually when `autoConnect: false` is used.

**Usage:**

```csharp
var app = builder.Build();

// If autoConnect: false was used with AddHorse
app.Services.UseHorse();

app.Run();
```

**With Keyed Services:**

```csharp
app.Services.UseHorse("primary");
```

---

### 2. IHostBuilder Extensions

#### AddHorse (Simple Configuration)

Horse Client configuration on IHostBuilder.

**Usage:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200")
               .SetClientName("Consumer-1")
               .SetClientType("Consumer")
               .AddTransientConsumers(typeof(Program));
    })
    .Build();

await host.RunAsync();
```

**Parameters:**
- `configureDelegate`: HorseClientBuilder configuration delegate
- `autoConnect`: Auto connect on startup (default: true)

#### AddHorse (Advanced Configuration)

Configuration with access to Configuration, Environment, and Services.

**Usage:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse((builder, config, env, services) =>
    {
        var horseOptions = config.GetSection("HorseOptions").Get<HorseOptions>();
        
        builder.AddHost(horseOptions.Host)
               .SetClientName(horseOptions.ClientName);

        if (env.IsDevelopment())
        {
            builder.SetReconnectWait(TimeSpan.FromSeconds(2));
        }

        // Additional services
        services.AddSingleton<MyCustomService>();
    })
    .Build();

await host.RunAsync();
```

#### AddHorse (Keyed Services)

Host configuration with Keyed Services.

**Usage:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse("primary", builder =>
    {
        builder.AddHost("horse://primary:26200");
    })
    .AddHorse("secondary", builder =>
    {
        builder.AddHost("horse://secondary:26200");
    })
    .Build();

await host.RunAsync();
```

---

### 3. IHostApplicationBuilder Extensions

For modern minimal API style applications.

#### AddHorse

**Usage (Minimal API):**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("WebAPI-Consumer")
                .AddTransientConsumers(typeof(Program));
});

var app = builder.Build();
app.Run();
```

**Usage (Worker Service):**

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.AddHorse((horseBuilder, config, env, services) =>
{
    var connectionString = config.GetConnectionString("Horse");
    
    horseBuilder.AddHost(connectionString)
                .SetClientType("BackgroundWorker")
                .AddTransientConsumers(typeof(Program));
    
    services.AddHostedService<MyBackgroundService>();
});

var host = builder.Build();
await host.RunAsync();
```

---

### 4. IHost Extensions

#### UseHorse

Manually starts the Horse connection.

**Usage:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200");
    }, autoConnect: false)  // autoConnect = false
    .Build();

// Connect when application is ready
await host.StartAsync();
host.UseHorse();

await host.WaitForShutdownAsync();
```

**With Keyed Services:**

```csharp
host.UseHorse("primary");
host.UseHorse("secondary");
```

---

### 5. HorseClientBuilder Methods

#### UseGracefulShutdown

Waits for ongoing consume operations to complete when the application is shutting down. This method is defined directly in `HorseClientBuilder` (not an extension method).

**Simple Usage:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200")
               .UseGracefulShutdown(
                   minWait: TimeSpan.FromSeconds(1),
                   maxWait: TimeSpan.FromSeconds(30)
               );
    })
    .Build();

await host.RunAsync();
```

**Usage with Callback:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200")
               .UseGracefulShutdown(
                   minWait: TimeSpan.FromSeconds(2),
                   maxWait: TimeSpan.FromSeconds(30),
                   shuttingDownAction: async () =>
                   {
                       // Code to execute before shutdown begins
                       Console.WriteLine("Application is shutting down...");
                   }
               );
    })
    .Build();

await host.RunAsync();
```

**Usage with ServiceProvider Callback:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .AddHorse(builder =>
    {
        builder.AddHost("horse://localhost:26200")
               .UseGracefulShutdown(
                   minWait: TimeSpan.FromSeconds(2),
                   maxWait: TimeSpan.FromSeconds(30),
                   shuttingDownAction: async (serviceProvider) =>
                   {
                       var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                       logger.LogWarning("Application is shutting down, waiting for operations to complete...");
                       
                       // Update health check status
                       var healthService = serviceProvider.GetService<IHealthService>();
                       await healthService?.SetUnhealthyAsync();
                   }
               );
    })
    .Build();

await host.RunAsync();
```

**Parameters:**
- `minWait`: Minimum wait time. The host will wait at least this duration, even if everything is done
- `maxWait`: Maximum wait time. If consume operations are still in progress, they will be canceled after this duration
- `shuttingDownAction`: Function to execute before shutdown begins (optional). Can be `Func<Task>` or `Func<IServiceProvider, Task>`

**Supported Shutdown Signals:**
- `AppDomain.ProcessExit` - When process is exiting
- `Console.CancelKeyPress` - When Ctrl+C is pressed
- `SIGTERM` / `SIGINT` - POSIX signals (Linux/macOS, Docker stop, kill command)
- `IHostApplicationLifetime.ApplicationStopping` - When host is stopping (via `IHostedService`)

**Single Execution Guarantee:**
Graceful shutdown is guaranteed to run only once, regardless of which signal triggers it first. This prevents duplicate unsubscribe operations and callback executions.

**How It Works:**
1. When the application receives a stop signal (any of the above), all Queue and Channel subscriptions are canceled
2. If `shuttingDownAction` exists, it is executed
3. Waits for `minWait` duration
4. Waits for active consume operations to complete (up to `maxWait` duration)
5. The application shuts down gracefully

**Keyed Services Support:**
Graceful shutdown works automatically with keyed services:

```csharp
builder.AddHorse("primary", config =>
{
    config.AddHost("horse://primary:26200")
          .UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
});

builder.AddHorse("secondary", config =>
{
    config.AddHost("horse://secondary:26200")
          .UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
});
```

---

## Complete Examples

### Example 1: ASP.NET Core Web API with Queue Consumer

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Extensions.Client;

var builder = WebApplication.CreateBuilder(args);

// Add Horse Client
builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("WebAPI-Consumer")
                .SetClientType("Consumer")
                .AutoSubscribe(true)
                .AutoAcknowledge(false)
                .AddTransientConsumers(typeof(Program))
                .UseGracefulShutdown(
                    TimeSpan.FromSeconds(2), 
                    TimeSpan.FromSeconds(30));
});

// Other services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Queue Consumer
public class OrderCreatedConsumer : IQueueConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IOrderService _orderService;

    public OrderCreatedConsumer(
        ILogger<OrderCreatedConsumer> logger, 
        IOrderService orderService)
    {
        _logger = logger;
        _orderService = orderService;
    }

    public async Task Consume(HorseMessage message, OrderCreatedEvent model, HorseClient client)
    {
        _logger.LogInformation("Processing order: {OrderId}", model.OrderId);
        
        try
        {
            await _orderService.ProcessOrderAsync(model);
            await client.Queue.Ack(message);
            _logger.LogInformation("Order processed successfully: {OrderId}", model.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order: {OrderId}", model.OrderId);
            await client.Queue.Nack(message, NackReason.Error);
        }
    }
}

public class OrderCreatedEvent
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Example 2: Worker Service with Channel Subscriber

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Extensions.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.AddHorse((horseBuilder, config, env, services) =>
{
    var horseOptions = config.GetSection("Horse").Get<HorseOptions>();
    
    horseBuilder.AddHost(horseOptions.Host)
                .SetClientName(horseOptions.ClientName)
                .SetClientType("ChannelSubscriber")
                .SetClientToken(horseOptions.Token)
                .SetReconnectWait(TimeSpan.FromSeconds(5))
                .AddTransientChannelSubscribers(typeof(Program))
                .UseGracefulShutdown(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(20));
    
    services.AddSingleton<INotificationService, NotificationService>();
});

var host = builder.Build();
await host.RunAsync();

// Channel Subscriber
public class NotificationSubscriber : IChannelSubscriber<NotificationMessage>
{
    private readonly ILogger<NotificationSubscriber> _logger;
    private readonly INotificationService _notificationService;

    public NotificationSubscriber(
        ILogger<NotificationSubscriber> logger,
        INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task Handle(HorseMessage message, NotificationMessage model, HorseClient client)
    {
        _logger.LogInformation("Received notification for user: {UserId}", model.UserId);
        await _notificationService.SendAsync(model);
    }
}

public class NotificationMessage
{
    public string UserId { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
}

// appsettings.json
{
  "Horse": {
    "Host": "horse://localhost:26200",
    "ClientName": "NotificationService",
    "Token": "your-auth-token"
  }
}
```

### Example 3: Multiple Horse Servers (Keyed Services)

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Primary Server
builder.AddHorse("primary", horseBuilder =>
{
    horseBuilder.AddHost("horse://primary-server:26200")
                .SetClientName("Multi-Primary")
                .AddTransientConsumers(typeof(Program));
});

// Secondary Server  
builder.AddHorse("secondary", horseBuilder =>
{
    horseBuilder.AddHost("horse://secondary-server:26200")
                .SetClientName("Multi-Secondary")
                .AddTransientConsumers(typeof(Program));
});

builder.Services.AddControllers();

var app = builder.Build();

// Connect to both servers
app.Services.UseHorse("primary");
app.Services.UseHorse("secondary");

app.MapControllers();
app.Run();

[ApiController]
[Route("api/[controller]")]
public class MessagingController : ControllerBase
{
    private readonly HorseClient _primaryClient;
    private readonly HorseClient _secondaryClient;

    public MessagingController(
        [FromKeyedServices("primary")] HorseClient primaryClient,
        [FromKeyedServices("secondary")] HorseClient secondaryClient)
    {
        _primaryClient = primaryClient;
        _secondaryClient = secondaryClient;
    }

    [HttpPost("send-to-primary")]
    public async Task<IActionResult> SendToPrimary([FromBody] MyMessage message)
    {
        var result = await _primaryClient.Queue.PushJson(message);
        return Ok(new { result.Code });
    }

    [HttpPost("send-to-secondary")]
    public async Task<IActionResult> SendToSecondary([FromBody] MyMessage message)
    {
        var result = await _secondaryClient.Queue.PushJson(message);
        return Ok(new { result.Code });
    }
}
```

### Example 4: Direct Message Handler

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Extensions.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("DirectHandler")
                .AddTransientDirectHandlers(typeof(Program));
});

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();

// Request Handler
public class GetUserRequestHandler : IDirectHandler<GetUserRequest, GetUserResponse>
{
    private readonly ILogger<GetUserRequestHandler> _logger;
    private readonly IUserRepository _userRepository;

    public GetUserRequestHandler(
        ILogger<GetUserRequestHandler> logger,
        IUserRepository userRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
    }

    public async Task<GetUserResponse> Handle(GetUserRequest request, HorseMessage rawMessage, HorseClient client)
    {
        _logger.LogInformation("Getting user: {UserId}", request.UserId);
        
        var user = await _userRepository.GetByIdAsync(request.UserId);
        
        return new GetUserResponse
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }
}

public class GetUserRequest
{
    public string UserId { get; set; }
}

public class GetUserResponse
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// Sending request
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly HorseClient _horseClient;

    public UserController(HorseClient horseClient)
    {
        _horseClient = horseClient;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        var request = new GetUserRequest { UserId = userId };
        
        var response = await _horseClient.Direct.RequestJson<GetUserRequest, GetUserResponse>(
            request,
            "target-client-id",
            timeoutDuration: TimeSpan.FromSeconds(10));
        
        if (response.Code == HorseResultCode.Ok)
            return Ok(response.Model);
        
        return StatusCode(500, "Request failed");
    }
}
```

### Example 5: Manual Connection Management

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;

var builder = WebApplication.CreateBuilder(args);

// autoConnect: false - Manual connection
builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("ManualConnect")
                .OnConnected(client =>
                {
                    Console.WriteLine("✅ Connected to Horse Server");
                })
                .OnDisconnected(client =>
                {
                    Console.WriteLine("❌ Disconnected from Horse Server");
                })
                .OnError(exception =>
                {
                    Console.WriteLine($"⚠️ Error: {exception.Message}");
                });
}, autoConnect: false);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

// Connect when application is ready
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine("Application started, connecting to Horse...");
    app.Services.UseHorse();
});

app.Run();
```

### Example 6: Advanced Usage with Interceptors

```csharp
using Horse.Messaging.Client;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("InterceptorExample")
                .AddTransientConsumers(typeof(Program))
                .UseBeforeInterceptor<LoggingInterceptor>()
                .UseAfterInterceptor<MetricsInterceptor>();
});

var app = builder.Build();
app.Run();

// Before Interceptor - Before message processing
public class LoggingInterceptor : IHorseMessageInterceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public async Task Intercept(HorseMessage message, IServiceProvider serviceProvider)
    {
        _logger.LogInformation(
            "📨 Message received - Queue: {Queue}, Type: {Type}", 
            message.Target, 
            message.ContentType);
        
        await Task.CompletedTask;
    }
}

// After Interceptor - After message processing
public class MetricsInterceptor : IHorseExecutedInterceptor
{
    private readonly ILogger<MetricsInterceptor> _logger;
    private readonly IMetricsService _metrics;

    public MetricsInterceptor(
        ILogger<MetricsInterceptor> logger,
        IMetricsService metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Intercept(
        HorseExecutionData data, 
        Exception exception, 
        IServiceProvider serviceProvider)
    {
        if (exception == null)
        {
            _metrics.IncrementSuccess(data.Message.Target);
            _logger.LogInformation("✅ Message processed successfully in {Duration}ms", 
                data.Elapsed.TotalMilliseconds);
        }
        else
        {
            _metrics.IncrementFailure(data.Message.Target);
            _logger.LogError(exception, "❌ Message processing failed in {Duration}ms", 
                data.Elapsed.TotalMilliseconds);
        }

        await Task.CompletedTask;
    }
}
```

---

## Best Practices

### 1. Lifecycle Selection

```csharp
// ✅ Transient - For stateless consumers (recommended)
builder.AddTransientConsumers(typeof(Program));

// ✅ Scoped - For database operations
builder.AddScopedConsumers(typeof(Program));

// ⚠️ Singleton - Use carefully (thread-safety required)
builder.AddSingletonConsumers(typeof(Program));
```

### 2. Error Handling

```csharp
public class SafeConsumer : IQueueConsumer<MyMessage>
{
    public async Task Consume(HorseMessage message, MyMessage model, HorseClient client)
    {
        try
        {
            await ProcessMessage(model);
            await client.Queue.Ack(message);
        }
        catch (ValidationException ex)
        {
            // Invalid message - no retry
            await client.Queue.Nack(message, NackReason.None);
        }
        catch (Exception ex)
        {
            // Error - requeue message
            await client.Queue.Nack(message, NackReason.Error);
        }
    }
}
```

### 3. Configuration Management

```csharp
// appsettings.json
{
  "Horse": {
    "Hosts": [
      "horse://primary:26200",
      "horse://secondary:26200"
    ],
    "ClientName": "MyService",
    "ClientType": "Consumer",
    "Token": "auth-token",
    "ReconnectWaitSeconds": 5,
    "ResponseTimeoutSeconds": 30
  }
}

// Configuration class
public class HorseConfiguration
{
    public string[] Hosts { get; set; }
    public string ClientName { get; set; }
    public string ClientType { get; set; }
    public string Token { get; set; }
    public int ReconnectWaitSeconds { get; set; }
    public int ResponseTimeoutSeconds { get; set; }
}

// Usage
builder.AddHorse((horseBuilder, config, env, services) =>
{
    var horseConfig = config.GetSection("Horse").Get<HorseConfiguration>();
    
    foreach (var host in horseConfig.Hosts)
    {
        horseBuilder.AddHost(host);
    }
    
    horseBuilder.SetClientName(horseConfig.ClientName)
                .SetClientType(horseConfig.ClientType)
                .SetClientToken(horseConfig.Token)
                .SetReconnectWait(TimeSpan.FromSeconds(horseConfig.ReconnectWaitSeconds))
                .SetResponseTimeout(TimeSpan.FromSeconds(horseConfig.ResponseTimeoutSeconds));
});
```

### 4. Graceful Shutdown Strategy

```csharp
builder.AddHorse(builder =>
{
    builder.UseGracefulShutdown(
        minWait: TimeSpan.FromSeconds(2),    // Wait at least 2 seconds
        maxWait: TimeSpan.FromSeconds(60),   // Wait maximum 60 seconds
        shuttingDownAction: async (sp) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            
            // Notify load balancer
            var healthCheck = sp.GetService<IHealthCheckService>();
            await healthCheck?.MarkUnhealthyAsync();
            
            logger.LogWarning("Graceful shutdown started. No new messages will be accepted.");
            
            // Wait 2 seconds (for load balancer to update)
            await Task.Delay(2000);
        });
});
```

**Note:** `UseGracefulShutdown` works in all scenarios:
- When host is stopped via `IHostApplicationLifetime`
- When application is terminated with Ctrl+C
- When process receives SIGTERM/SIGINT signals
- When application exits normally without calling `host.StopAsync()`

---

## Frequently Asked Questions

### When should autoConnect be false?

```csharp
// Scenario: Database migrations must be run before application starts
builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200");
}, autoConnect: false);

var app = builder.Build();

// Run migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Now connect to Horse
app.Services.UseHorse();

app.Run();
```

### Can multiple consumers listen to the same queue?

Yes! Horse automatically does load balancing:

```csharp
// Consumer 1
builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("Consumer-1")
                .AddTransientConsumers(typeof(Program));
});

// Consumer 2
builder.AddHorse(horseBuilder =>
{
    horseBuilder.AddHost("horse://localhost:26200")
                .SetClientName("Consumer-2")
                .AddTransientConsumers(typeof(Program));
});

// Both listen to the same queue and messages are distributed between them
```

### What is the connection string format?

```
horse://hostname:port
horse://username:password@hostname:port
horse://hostname:port?option1=value1&option2=value2
```

---

## Related Documentation

- [Horse.Messaging.Client](client.md)
- [Horse.Messaging.Server](overview.md)
- [Horse.Messaging.Protocol](overview.md)

## License

[MIT License](../../LICENSE)

## Support

- GitHub Issues: [horse-framework/horse-messaging/issues](https://github.com/horse-framework/horse-messaging/issues)
- Documentation: [horse-framework.com](https://horse-framework.com)

