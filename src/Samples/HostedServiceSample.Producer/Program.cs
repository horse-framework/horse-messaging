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
	Console.Write("Press enter to push message....");
	HorseResult result = await service.HorseClient.Queue.PushJson(model, false);
	Console.WriteLine(result.Code);
	Console.ReadLine();
}

internal class Program { }