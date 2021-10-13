using HostedServiceSample.Client;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args);
service.ConfigureHorseClient(clientBuilder => clientBuilder.AddTransientConsumer<TestQueueModelConsumer>());
service.Run();

internal class Program { }