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
    config.WithGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), (a) =>
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
    config.WithGracefulShutdown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), (a) =>
    {
        Console.WriteLine("Graceful shutdown initiated");
        return Task.CompletedTask;
    });
    config.AddTransientConsumer<TestEventConsumer>();
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
HorseResult? result = await bus.PushJson(new TestEvent(), true);
Console.WriteLine($"Message sent: {result.Code}");

while (!Test.MessageConsumed)
{
    Console.WriteLine("Waiting...");
    await Task.Delay(1000);
}

_ = producer.StopAsync();
_ = consumer.StopAsync();
_ = server.StopAsync();


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
    public Task Consume(HorseMessage message, TestEvent model, HorseClient client)
    {
        Console.WriteLine($"Received TestEvent: {model.Foo}");
        Test.MessageConsumed = true;
        return Task.CompletedTask;
    }
}

