using HostedServiceSample.Client;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args, "test-consumer");
service.ConfigureHorseClient(clientBuilder => clientBuilder.AddTransientConsumer<TestQueueModelConsumer>());
service.Run();