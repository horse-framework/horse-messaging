using HostedServiceSample.Client;
using HostedServiceSample.Consumer;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args, "test-consumer");
service.ConfigureHorseClient(clientBuilder =>
{
    clientBuilder.AddTransientConsumer<TestQueueModelConsumer>();
    clientBuilder.AddTransientConsumer<TestQueueModel2Consumer>();
});
service.Run();