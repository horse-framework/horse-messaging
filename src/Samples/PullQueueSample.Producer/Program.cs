using PullQueueSample.Client;
using PullQueueSample.Producer;

var service = HorseServiceFactory.Create<Program>(args, "test-producer");
_ = service.RunAsync();

while (service.HorseClient.IsConnected)
{
    Console.Write("Press enter to push message....");
    Console.ReadLine();
    var model = new TestQueueModel
    {
        Foo = "Emre",
        Bar = "Hizli"
    };
    var result = await service.HorseClient.Queue.PushJson(model, true);
    Console.WriteLine(result.Code);
}