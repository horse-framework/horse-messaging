using System.Threading;
﻿using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using infra.messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sample.All;

HostApplicationBuilder serverBuilder = Host.CreateApplicationBuilder();
serverBuilder.Services.AddHostedService<ServerHostedService>();
IHost server = serverBuilder.Build();

HostApplicationBuilder producerBuilder = Host.CreateApplicationBuilder();
producerBuilder.AddHorse(config =>
{
    config.AddHost("horse://localhost:2626");
    config.SetClientName("Sample.Producer");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.OnConnected(m => { Console.WriteLine("Connected to Horse server"); });
});
IHost producer = producerBuilder.Build();

HostApplicationBuilder consumerBuilder = Host.CreateApplicationBuilder();
consumerBuilder.AddHorse((config, configuration, _, services) =>
{
    services.AddHostedService<HorseClientService>();
    config.AddHost("horse://localhost:2626");
    config.SetClientName("Sample.Client");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.AddScopedConsumer<TestEventConsumer>("Free", 1, 50);
    config.AddScopedConsumer<TestEvent2Consumer>("Free", 1, 50);
    config.AddScopedConsumer<TestEvent3Consumer>(queueName => $"{queueName}-Free", true);
    config.AddScopedConsumer<TestEvent4Consumer>("Free", 1, 50);
    config.OnConnected(m => { Console.WriteLine("Connected to Horse server"); });
});

IHost consumer = consumerBuilder.Build();

_ = server.StartAsync();
_ = producer.StartAsync();
_ = consumer.StartAsync();

HorseClient producerClient = producer.Services.GetRequiredService<HorseClient>();
HorseClient consumerClient = consumer.Services.GetRequiredService<HorseClient>();

while (!producerClient.IsConnected || !consumerClient.IsConnected)
{
    Console.WriteLine("Waiting for clients...");
    await Task.Delay(1000);
}

IHorseQueueBus bus = producer.Services.GetRequiredService<IHorseQueueBus>();
try
{
    HorseResult? result = await bus.Push(new TestEvent(), true, partitionLabel: "Free");
    Console.WriteLine($"Message sent: {result.Code}");
}
catch (Exception ex)
{
    Console.WriteLine($"Push failed: {ex.Message}");
}

while (!Test.Message4Consumed)
{
    Console.WriteLine("Waiting...");
    await Task.Delay(1000);
}

// Wait for all in-flight consume operations to finish (including ack sends).
HorseClient cc = consumer.Services.GetRequiredService<HorseClient>();
int waitMs = 0;
while (cc.Queue.ActiveConsumeOperations > 0 && waitMs < 5000)
{
    await Task.Delay(100);
    waitMs += 100;
}

// Allow server-side ack processing and HDB flush to complete
await Task.Delay(2000);

await consumer.StopAsync();
await Task.Delay(500);
await producer.StopAsync();
await server.StopAsync();


Console.WriteLine("Exit");

internal static class Test
{
    public static bool MessageConsumed;
    public static bool Message2Consumed;
    public static bool Message3Consumed;
    public static bool Message4Consumed;
}

internal class TestEvent
{
    public string Foo { get; set; } = "Foo";
}

internal class Test2Event
{
    public string Foo { get; set; } = "Foo2";
}

[Acknowledge(QueueAckDecision.waitAcknowledge)]
internal class Test3Event
{
    public string Foo { get; set; } = "Foo3";
}

internal class Test4Event
{
    public string Foo { get; set; } = "Foo4";
}


[AutoAck]
[AutoNack]
internal class TestEventConsumer(IHorseQueueBus queueBus) : IQueueConsumer<TestEvent>
{
    public async Task Consume(HorseMessage message, TestEvent model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received TestEvent: {model.Foo}");
        Test.MessageConsumed = true;
        HorseResult? result = await queueBus.Push(new Test2Event(), true, partitionLabel: "Free", cancellationToken: cancellationToken);
        Console.WriteLine($"Message2 sent: {result.Code}");
    }
}

[AutoAck]
[AutoNack]
internal class TestEvent2Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test2Event>
{
    public async Task Consume(HorseMessage message, Test2Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test2Event: {model.Foo}");
        Test.Message2Consumed = true;
        HorseResult? result = await queueBus.Push("Test3Event-Free", new Test3Event(), true, partitionLabel: "tenant-id", cancellationToken: cancellationToken);
        Console.WriteLine($"Message3 sent: {result.Code}");
    }
}

[AutoAck]
[AutoNack]
internal class TestEvent3Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test3Event>
{
    public async Task Consume(HorseMessage message, Test3Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test3Event: {model.Foo}");
        Test.Message3Consumed = true;
        HorseResult? result = await queueBus.Push(new Test4Event(), true, partitionLabel: "Free", cancellationToken: cancellationToken);
        Console.WriteLine($"Message4 sent: {result.Code}");
    }
}

[AutoAck]
[AutoNack]
internal class TestEvent4Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test4Event>
{
    public Task Consume(HorseMessage message, Test4Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test4Event: {model.Foo}");
        Test.Message4Consumed = true;
        return Task.CompletedTask;
    }
}

