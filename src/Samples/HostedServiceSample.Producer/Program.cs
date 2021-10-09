using System;
using Horse.Messaging.Protocol;
using HostedServiceSample.Client;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args);
_ = service.RunAsync();


while (service.HorseClient.IsConnected)
{
	TestQueueModel model = new()
	{
		Foo = "Emre",
		Bar = "Hizli"
	};
	HorseResult result = await service.HorseClient.Queue.PushJson(model, false);
	Console.WriteLine(result.Code);
	Console.Write("Press enter to push next message....");
	Console.ReadLine();
}

internal class Program { }