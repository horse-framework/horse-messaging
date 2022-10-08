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
	string modelType = Console.ReadLine() ?? "0";
	var result = await PushQueueMessage(userId, modelType);
	// var result = await PushQueueMessageThroughRouter(userId, modelType);
	// var result = await PushRouteRequestMessage(userId);
	Console.WriteLine(result.Code);
}

async Task<HorseResult> PushQueueMessage(string userId, string modelType)
{
	object model = null;
	Dictionary<string, string> headers = new()
	{
		{ "UserId", userId },
	};
	switch (modelType)
	{
		case "0":
			model = new TestQueueModel()
			{
				Foo = "Emre",
				Bar = "Hizli"
			};
			break;
		case "1":
			model = new TestQueueModel2()
			{
				Foo = "Emre1",
				Bar = "Hizli2"
			};
			break;
	}

	return await service.HorseClient.Queue.PushJson(model, true, headers);
}

async Task<HorseResult> PushQueueMessageThroughRouter(string userId, string modelType)
{
	object model = null;
	Dictionary<string, string> headers = new()
	{
		{ "UserId", userId },
	};
	switch (modelType)
	{
		case "0":
			model = new TestQueueModel()
			{
				Foo = "Emre",
				Bar = "Hizli"
			};
			break;
		case "1":
			model = new TestQueueModel2()
			{
				Foo = "Emre1",
				Bar = "Hizli2"
			};
			break;
	}

	return await service.HorseClient.Router.PublishJson(model, true, headers);
}

async Task<HorseResult> PushRouteDirectMessage(string userId)
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

async Task<HorseResult<TestResponseModel>> PushRouteRequestMessage(string userId)
{
	TestRequestModel model = new();
	Dictionary<string, string> headers = new()
	{
		{ "UserId", userId },
	};
	return await service.HorseClient.Router.PublishRequestJson<TestRequestModel, TestResponseModel>(model, headers);
}