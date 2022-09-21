using PullQueueSample.Client;

var service = HorseServiceFactory.Create<Program>(args, "test-producer");
_ = service.RunAsync();