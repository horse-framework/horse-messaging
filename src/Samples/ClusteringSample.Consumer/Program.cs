using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;

namespace ClusteringSample.Consumer;

public class Foo
{
    public int No { get; set; }
}

class Program
{
    static async Task Main(string[] args)
    {
        HorseClient client = new HorseClient();
        client.AddHost("horse://localhost:26101");
        client.AddHost("horse://localhost:26102");
        client.AddHost("horse://localhost:26103");
            
        QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(client.Queue);
        registrar.RegisterConsumer<FooConsumer>();

        await client.ConnectAsync();

        while (true)
            Console.ReadLine();
    }
}