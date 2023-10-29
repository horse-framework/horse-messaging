using System;
using Horse.Messaging.Client;
using Horse.Messaging.Extensions.Client;
using Horse.Messaging.Server.OverWebSockets;
using Microsoft.Extensions.Hosting;

HorseClient client = null;

IHost host = Host.CreateDefaultBuilder(args)
    .UseHorse(cfg =>
    {
        cfg.AddHost("localhost:8080");
        cfg.SetClientName("Test");
        cfg.UseHorseOverWebSockets();
        cfg.OnConnected(async c =>
        {
            Console.WriteLine("Connected");
            client = c;
        });
    })
    .Build();

host.Start();


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