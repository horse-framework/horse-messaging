using Horse.Messaging.Client.Queues;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
_ = producer.StartAsync();

IHorseQueueBus bus = producer.Services.GetRequiredService<IHorseQueueBus>();

while (true)
{
    Console.WriteLine("Press any key to push message...");
    Console.ReadLine();
    try
    {
        HorseResult? result = await bus.Push(new TestEvent(), true, partitionLabel: "Free");
        Console.WriteLine($"Message sent: {result.Code}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Push failed: {ex.Message}");
    }
}

