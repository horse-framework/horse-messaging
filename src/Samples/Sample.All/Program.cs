﻿using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
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
    config.UseQueueName(ctx => $"{ctx.QueueName}-Free");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.OnConnected(m =>
    {
        Console.WriteLine("Connected to Horse server");
    });
});
IHost producer = producerBuilder.Build();

HostApplicationBuilder consumerBuilder = Host.CreateApplicationBuilder();
consumerBuilder.AddHorse(config =>
{
    config.AddHost("horse://localhost:2626");
    config.SetClientName("Sample.Client");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.AddTransientConsumer<TestEventConsumer>((queueName) => $"{queueName}-Free", null);
    config.OnConnected(m =>
    {
        Console.WriteLine("Connected to Horse server");
    });
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
    HorseResult? result = await bus.Push(new TestEvent(), true, partitionLabel: "sample-tenant");
    Console.WriteLine($"Message sent: {result.Code}");
}
catch (Exception ex)
{
    Console.WriteLine($"Push failed: {ex.Message}");
}

while (!Test.MessageConsumed)
{
    Console.WriteLine("Waiting...");
    await Task.Delay(1000);
}

_ = producer.StopAsync();
_ = consumer.StopAsync();
_ = server.StopAsync();

Console.WriteLine("Exit");

internal static class Test
{
    public static bool MessageConsumed;
}

internal class TestEvent
{
    public string Foo { get; set; } = "Foo";
}

[AutoAck]
[AutoNack]
internal class TestEventConsumer : IQueueConsumer<TestEvent>
{
    public Task Consume(HorseMessage message, TestEvent model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received TestEvent: {model.Foo}");
        Test.MessageConsumed = true;
        return Task.CompletedTask;
    }
}

