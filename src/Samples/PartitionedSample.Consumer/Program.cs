﻿using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.Hosting;

for (int i = 0; i < args.Length - 1; i++)
    if (args[i].Equals("--pl", StringComparison.OrdinalIgnoreCase))
    {
        Label.Value = args[i + 1];
        continue;
    }

HostApplicationBuilder consumer1Builder = Host.CreateApplicationBuilder();
consumer1Builder.AddHorse(config =>
{
    config.AddHost("horse://localhost:2626");
    config.SetClientName("Sample.Client");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.AddScopedConsumer<TestEventConsumer>(Label.Value, 3, 50);
    config.AddScopedConsumer<TestEvent2Consumer>(Label.Value, 3, 50);
    config.AddScopedConsumer<TestEvent3Consumer>(queueName => $"{queueName}-Free", enterWorkerPool: false);
    config.AddScopedConsumer<TestEvent4Consumer>(Label.Value, 3, 50);
    config.OnConnected(m => { Console.WriteLine("Connected to Horse server"); });
});
IHost consumer1 = consumer1Builder.Build();
consumer1.Run();

[AutoNack]
internal class TestEventConsumer(IHorseQueueBus queueBus) : IQueueConsumer<TestEvent>
{
    public async Task Consume(HorseMessage message, TestEvent model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received TestEvent: {model.Foo}");
        Test.MessageConsumed = true;
        HorseResult? result = await queueBus.Push(new Test2Event(), true, partitionLabel: Label.Value, cancellationToken: cancellationToken);
        Console.WriteLine($"Message2 sent: {result.Code}");
        HorseMessage? ack = message.CreateAcknowledge();

        if (ack != null)
            client.Send(ack, message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent2Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test2Event>
{
    public async Task Consume(HorseMessage message, Test2Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test2Event: {model.Foo}");
        Test.Message2Consumed = true;
        Dictionary<string, string> headers = new() { { "tenant-id", "tenant-id-value" } };
        HorseResult? result = await queueBus.Push("Test3Event-Free", new Test3Event(), true, headers, "tenant-id", cancellationToken);
        Console.WriteLine($"Message3 sent: {result.Code}");
        HorseMessage? ack = message.CreateAcknowledge();

        if (ack != null)
            client.Send(ack, message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent3Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test3Event>
{
    public async Task Consume(HorseMessage message, Test3Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test3Event: {model.Foo}");
        Test.Message3Consumed = true;
        HorseResult? result = await queueBus.Push(new Test4Event(), true, partitionLabel: Label.Value, cancellationToken: cancellationToken);
        Console.WriteLine($"Message4 sent: {result.Code}");
        HorseMessage? ack = message.CreateAcknowledge();

        if (ack != null)
            client.Send(ack, message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent4Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test4Event>
{
    public Task Consume(HorseMessage message, Test4Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test4Event: {model.Foo}");
        Test.Message4Consumed = true;
        HorseMessage? ack = message.CreateAcknowledge();

        if (ack != null)
            client.Send(ack, message.Headers.ToList());
        return Task.CompletedTask;
    }
}

public static class Label
{
    public static string Value = "";
}
