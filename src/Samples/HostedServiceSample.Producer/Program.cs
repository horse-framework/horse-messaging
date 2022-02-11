using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using HostedServiceSample.Client;
using HostedServiceSample.Producer;

var service = HorseServiceFactory.Create<Program>(args, "test-producer");
_ = service.RunAsync();


while (service.HorseClient.IsConnected)
{
	Console.Write("Press enter to push message....");
	string userId = Console.ReadLine();
	var result = await PushQueueMessage(userId);
	Console.WriteLine(result.Code);
}

async Task<HorseResult> PushQueueMessage(string userId)
{
	TestQueueModel model = new()
	{
		Foo = "Emre",
		Bar = "Hizli"
	};
	Dictionary<string, string> headers = new()
	{
		{ "UserId", userId },
	};
	return await service.HorseClient.Queue.PushJson(model, true, headers);
}

async Task<HorseResult> PushRouteMessage(string userId)
{
	TestDirectModel model = new()
	{
		Foo = "Emre",
		Bar = "Hizli"
	};
	Dictionary<string, string> headers = new()
	{
		{ "UserId", userId },
	};
	return await service.HorseClient.Router.PublishJson(model, false, headers);
}