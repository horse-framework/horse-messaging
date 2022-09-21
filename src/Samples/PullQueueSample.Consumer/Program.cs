using Horse.Messaging.Client.Queues;
using PullQueueSample.Client;
using PullQueueSample.Consumer;

var service = HorseServiceFactory.Create<Program>(args, "test-consumer");
service.ConfigureHorseClient(clientBuilder => { clientBuilder.AddTransientConsumer<TestQueueModelConsumer>(); });
_ = service.RunAsync();

while (service.HorseClient.IsConnected)
{
    Console.Write("Press enter to pull message....");
    Console.ReadLine();
    var result = await service.HorseClient.Queue.Pull(new PullRequest
    {
        Queue = nameof(TestQueueModel)
    });
    Console.WriteLine(result.ReceivedCount);
}