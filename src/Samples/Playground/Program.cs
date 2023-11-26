using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues.Annotations;

namespace Playground;

[QueueName("Test")]
public class Foo
{
    public int X { get; set; } = 1;
}

class Program
{
    static async Task Main(string[] args)
    {
        HorseClient client = new HorseClient();
        client.MessageSerializer = new SystemJsonContentSerializer();
        await client.ConnectAsync("localhost:2626");

        int s = 0;
        int c = 0;
        Thread thread = new Thread(() =>
        {
            Thread.Sleep(500);
            while (true)
            {
                Thread.Sleep(1000);
                s++;
                Console.WriteLine(s + " - " + c);
            }
        });

        thread.Start();

        List<Foo> items = new List<Foo>();
        for (int i = 0; i < 5000; i++)
            items.Add(new Foo {X = i});

        while (true)
        {
            client.Queue.PushBulkJson("Test", items, (message, b) =>
            {
                if (b)
                    c++;
            });
        }
    }
}