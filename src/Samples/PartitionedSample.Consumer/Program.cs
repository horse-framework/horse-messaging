using System.Threading;
﻿﻿using Horse.Messaging.Client;
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
    public async Task Consume(ConsumeContext<TestEvent> context)
    {
        Console.WriteLine($"Received TestEvent: {context.Model.Foo}");
        Test.MessageConsumed = true;
        HorseResult? result = await queueBus.Push(new Test2Event(), true, null, Label.Value, context.CancellationToken);
        Console.WriteLine($"Message2 sent: {result.Code}");
        HorseMessage? ack = context.Message.CreateAcknowledge();

        if (ack != null)
            context.Client.Send(ack, context.Message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent2Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test2Event>
{
    public async Task Consume(ConsumeContext<Test2Event> context)
    {
        Console.WriteLine($"Received Test2Event: {context.Model.Foo}");
        Test.Message2Consumed = true;
        Dictionary<string, string> headers = new() { { "tenant-id", "tenant-id-value" } };
        HorseResult? result = await queueBus.Push("Test3Event-Free", new Test3Event(), true, headers, "tenant-id", context.CancellationToken);
        Console.WriteLine($"Message3 sent: {result.Code}");
        HorseMessage? ack = context.Message.CreateAcknowledge();

        if (ack != null)
            context.Client.Send(ack, context.Message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent3Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test3Event>
{
    public async Task Consume(ConsumeContext<Test3Event> context)
    {
        Console.WriteLine($"Received Test3Event: {context.Model.Foo}");
        Test.Message3Consumed = true;
        HorseResult? result = await queueBus.Push(new Test4Event(), true, null, Label.Value, context.CancellationToken);
        Console.WriteLine($"Message4 sent: {result.Code}");
        HorseMessage? ack = context.Message.CreateAcknowledge();

        if (ack != null)
            context.Client.Send(ack, context.Message.Headers.ToList());
    }
}

[AutoNack]
internal class TestEvent4Consumer(IHorseQueueBus queueBus) : IQueueConsumer<Test4Event>
{
    public Task Consume(ConsumeContext<Test4Event> context)
    {
        Console.WriteLine($"Received Test4Event: {context.Model.Foo}");
        Test.Message4Consumed = true;
        HorseMessage? ack = context.Message.CreateAcknowledge();

        if (ack != null)
            context.Client.Send(ack, context.Message.Headers.ToList());
        return Task.CompletedTask;
    }
}

public static class Label
{
    public static string Value = "";
}
