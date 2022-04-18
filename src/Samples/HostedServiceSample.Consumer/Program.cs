using HostedServiceSample.Client;
using HostedServiceSample.Consumer;

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