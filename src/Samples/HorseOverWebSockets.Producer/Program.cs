using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Server.OverWebSockets;
using Microsoft.Extensions.Hosting;

HorseClient client = null;

IHost host = Host.CreateDefaultBuilder(args)
    .UseHorse(cfg =>
    {
        cfg.AddHost("ws://localhost:8080");
        cfg.SetClientName("Test");
        cfg.UseHorseOverWebSockets();
        cfg.OnConnected(async c =>
        {
            client = c;
            await Task.Delay(2500);
            await c.Channel.Subscribe("Test", false);
            await Task.Delay(2500);
            string abc = new string('x', 60000);
            await c.Channel.PublishString("Test", abc);
        });
        cfg.OnMessageReceived(msg => Console.WriteLine(msg.ToString().Substring(0, 14)));
    })
    .Build();

host.Start();


while (true)
{
    await Task.Delay(5000);
    //   await client.Channel.Subscribe("Test", true);
}

Console.ReadLine();
Console.ReadLine();

while (true)
{
    string line = Console.ReadLine();
    if (client == null)
    {
        Console.WriteLine("Client is null");
        continue;
    }

    var result = await client.Queue.Push("Foo", line, true);
    Console.WriteLine("result: " + result.Code);
}