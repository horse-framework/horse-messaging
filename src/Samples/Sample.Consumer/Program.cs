using System;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;

namespace Sample.Consumer;

class Program
{
    static async Task Main(string[] args)
    {
        HorseClientBuilder builder = new HorseClientBuilder();

        HorseClient client = builder.AddHost("horse://localhost:2626")
            .SetClientName("test")
            .SetClientType("test")
            .AddSingletonConsumers(typeof(Program))
            /*
           .ConfigureModels(cfg => //cfg.UseQueueName(type => "Username1")
                               cfg.UseConsumerAck()
                               .AddMessageHeader("Sender-Client-Name", "MyName")
                               .SetPutBackDelay(TimeSpan.FromSeconds(10)))*/
            .Build();

        client.MessageReceived += (horseClient, message) => Console.WriteLine(message.GetStringContent());
        
        client.SmartHealthCheck = false;
        client.PingInterval = TimeSpan.FromSeconds(10);
        await client.ConnectAsync();

        while (true)
        {
            Console.ReadLine();
            await client.Direct.SendJsonByType("Test1", 123, new ModelA(), true);
        }

        var subs = await client.Queue.Subscribe("model-g", true);
        Console.WriteLine("subs: " + subs.Code);

        while (true)
        {
            await Task.Delay(250);
            Console.ReadLine();
            await Task.Delay(250);

            var response = await client.Queue.Pull(new PullRequest
            {
                Queue = "model-g",
                Count = 1,
                ClearAfter = ClearDecision.None,
                GetQueueMessageCounts = false,
                Order = MessageOrder.Default
            }, async (i, message) => { await client.SendAck(message); });

            Console.WriteLine($"pull response is {response.Status} and received {response.ReceivedCount} messages.");
        }
    }
}