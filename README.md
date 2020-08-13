# Twino MQ

[![NuGet](https://img.shields.io/nuget/v/Twino.MQ?label=server%20nuget)](https://www.nuget.org/packages/Twino.MQ)
[![NuGet](https://img.shields.io/nuget/v/Twino.Client.TMQ?label=client%20nuget)](https://www.nuget.org/packages/Twino.Client.TMQ)
[![NuGet](https://img.shields.io/nuget/v/Twino.Extensions.ConsumerFactory?label=extensions%20nuget)](https://www.nuget.org/packages/Twino.Extensions.ConsumerFactory)

* Twino MQ is a .NET Core messaging queue and communication framework.
* Twino MQ is not only queue messaging server. In provides direct messages, requests, responses and getting server and client informations.
* Twino MQ has many queue messaging structure: Broadcast, Push, Pull, Cache.
* Clients can subscribe events and gets information when another client is connected or does something.

If you want to go further about Twino MQ infrastructure you can read specification and documentation from [here](https://github.com/twino-framework/twino-mq/blob/master/docs/twino-mq.pdf)<br><br>

### Very Quick Messaging Queue Server Example

    class Program
    {
        static Task Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(cfg => cfg.UseJustAllowDeliveryHandler());
            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }
    
### Very Quick Server Example with Persistent Queues

    class Program
    {
        static Task Main(string[] args)
        {
            TwinoServer server = new TwinoServer();
            TwinoMQ mq = server.UseTwinoMQ(cfg => cfg
                                                  .AddPersistentQueues(q => q.KeepLastBackup())
                                                  .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived));

            await mq.LoadPersistentQueues();
            server.Start(22200);
            await server.BlockWhileRunningAsync();
        }
    }

### Consumer without ConsumerFactory Implementation

Implementation

        static async Task Main(string[] args)
        {
            TmqStickyConnector connector = new TmqStickyConnector(TimeSpan.FromSeconds(1));
            connector.AutoJoinConsumerChannels = true;
            connector.InitJsonReader();
            connector.Consumer.RegisterAssemblyConsumers(typeof(Program));
            connector.AddHost("tmq://127.0.0.1:22200");
            connector.Run();
            while (true)
                await Task.Delay(1000);
        }

Model

    [QueueId(100)]
    [ChannelName("model-a")]
    public class ModelA
    {
        public string Foo { get; set; }
    }

Consumer

    [AutoAck]
    [AutoNack]
    public class QueueConsumerA : IQueueConsumer<ModelA>
    {
        public Task Consume(TmqMessage message, ModelA model, TmqClient client)
        {
            Console.WriteLine("Model A Consumed");
            return Task.CompletedTask;
        }
    }


### Consumer with ConsumerFactory Implementation

Model and Consumer same with example above. The Implementation change is here:

    services.UseTwinoBus(cfg => cfg.AddHost("tmq://127.0.0.1:22200")
                                   .AddTransientConsumers(typeof(Program)));


If you use a service provider, you can inject other services to consumer objects.


### Sending Messages as Producer

Twino accepts producers and consumers as client. Each client can be producer and consumer at same time. With ConsumerFactory implementation, you can inject ITwinoBus interface for being producer at same time. If you want to create only producer, you can skip Add..Consumers methods.

     ITwinoBus bus; //injected from somewhere
     //or you can do it with TmqStickyConnector object

     //push to a queue
     await bus.PushJson(new ModelA(), false);

     //publish to a router
     await bus.PublishJson(new ModelA(), false);

     //to a direct target, ModelA required DirectTarget attribute
     await bus.SendDirectJsonAsync(new ModelA(), false);



## Thanks

Thanks to JetBrains for open source license to use on this project.

[![jetbrains](https://user-images.githubusercontent.com/21208762/90192662-10043700-ddcc-11ea-9533-c43b99801d56.png)](https://www.jetbrains.com/?from=twino-framework)
