using System;
using Horse.Messaging.Extensions.Client;
using HostedServiceSample.Client;
using HostedServiceSample.Consumer;
using Microsoft.Extensions.Hosting;

IHorseService service = HorseServiceFactory.Create<Program>(args, "test-consumer");
service.ConfigureHorseClient(clientBuilder =>
{
    clientBuilder.AddTransientConsumer<TestQueueModelConsumer>();
    clientBuilder.AddTransientConsumer<TestQueueModel2Consumer>();
    clientBuilder.AddTransientConsumer<SerializedExceptionConsumer>();
    clientBuilder.AddTransientDirectHandler<TestDirectModelHandler>();
    clientBuilder.AddTransientDirectHandler<TestRequestModelHandler>();
});
service.Run();

/*
IHost host = Host.CreateDefaultBuilder()
    .UseHorse((context, cfg) =>
    {
        cfg.AddHost("horse://localhost");
        cfg.AddTransientConsumers(typeof(Program));
    })
    .Build();

await host.RunAsync();
*/