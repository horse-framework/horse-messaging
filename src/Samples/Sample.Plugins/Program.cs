// See https://aka.ms/new-console-template for more information

using System.Data;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Server;

HorseRider rider = HorseRiderBuilder.Create()
    .ConfigureQueues(cfg =>
    {
        cfg.Options.Type = QueueType.Push;
        cfg.Options.MessageLimit = 400;
        cfg.Options.AutoQueueCreation = true;
        cfg.UseMemoryQueues();
    })
    .Build();

try
{
    await rider.Plugin.AddAssemblyPlugins(typeof(Program).Assembly);
}
catch (DuplicateNameException)
{
    //assembly already added, skip.
}

HorseServer server = new HorseServer();
server.Options.PingInterval = 10;
server.UseRider(rider);
server.Start(2626);

await Task.Delay(250);
HorseClient client = new HorseClient();
client.AddHost("localhost:2626");
await client.ConnectAsync();

while (true)
{
    Console.Write("Plugin Name: ");
    string pluginName = Console.ReadLine();

    HorseMessage msg = new HorseMessage(MessageType.Plugin, pluginName);
    msg.SetStringContent("This is a sample plugin request message");
    HorseResult result = await client.SendAndGetAck(msg);
    Console.WriteLine($"Result code is {result.Code}");
    if (result.Message != null)
        Console.WriteLine($"Response message is: {result.Message.GetStringContent()}");

    Console.WriteLine("Press enter to test again");
    Console.ReadLine();
}