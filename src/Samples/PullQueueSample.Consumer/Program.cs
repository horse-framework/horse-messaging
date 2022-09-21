using PullQueueSample.Client;
using PullQueueSample.Consumer;

IHorseService service = HorseServiceFactory.Create<Program>(args, "test-consumer");
service.ConfigureHorseClient(clientBuilder => { clientBuilder.AddTransientConsumer<TestQueueModelConsumer>(); });
service.Run();