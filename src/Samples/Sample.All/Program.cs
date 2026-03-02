using Horse.Messaging.Client;
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
    config.OnConnected(m => { Console.WriteLine("Connected to Horse server"); });
});
IHost producer = producerBuilder.Build();

HostApplicationBuilder consumerBuilder = Host.CreateApplicationBuilder();
consumerBuilder.AddHorse(config =>
{
    config.AddHost("horse://localhost:2626");
    config.SetClientName("Sample.Client");
    config.UseQueueName(ctx => $"{ctx.QueueName}-Free");
    config.UseGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), () =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.AddTransientConsumer<TestEventConsumer>(queueName => $"{queueName}-Free", null);
    config.AddTransientConsumer<TestEvent2Consumer>(queueName => $"{queueName}-Free", null);
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
    HorseResult? result = await bus.Push(new TestEvent(), true, partitionLabel: "sample-tenant");
    Console.WriteLine($"Message sent: {result.Code}");
}
catch (Exception ex)
{
    Console.WriteLine($"Push failed: {ex.Message}");
}

while (!Test.Message2Consumed)
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
    public static bool Message2Consumed;
}

internal class TestEvent
{
    public string Foo { get; set; } = "Foo";
}

internal class Test2Event
{
    public string Foo2 { get; set; } = "Foo2";
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
        HorseResult? result = await queueBus.Push(new Test2Event(), true, partitionLabel: "sample-tenant", cancellationToken: cancellationToken);
        Console.WriteLine($"Message2 sent: {result.Code}");
    }
}

[AutoAck]
[AutoNack]
internal class TestEvent2Consumer : IQueueConsumer<Test2Event>
{
    public Task Consume(HorseMessage message, Test2Event model, HorseClient client,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Received Test2Event: {model.Foo2}");
        Test.Message2Consumed = true;
        return Task.CompletedTask;
    }
}