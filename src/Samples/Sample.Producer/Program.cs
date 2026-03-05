using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;

namespace Sample.Producer;

public class TestQuery
{
    public string Foo { get; set; }
}

public class TestCommand
{
    public string Foo { get; set; }
}

public class TestEvent
{
    public string Foo { get; set; }
}

public class TestQueryResult
{
    public string Bar { get; set; }
}

class Program
{
    static async Task Main(string[] args)
    {
        HorseClientBuilder builder = new HorseClientBuilder();
        builder.AddHost("horse://localhost:2626");
        builder.SetClientType("Test1");
        builder.SetClientName("Test1-Name");

        builder.SetResponseTimeout(TimeSpan.FromSeconds(300));
        HorseClient client = builder.Build();
        client.NoDelay = false;
        client.Connected += horseClient => Console.WriteLine("connected");
        client.ResponseTimeout = TimeSpan.FromSeconds(300);
        client.ReconnectWait = TimeSpan.FromHours(1);

        client.MessageReceived += (horseClient, message) => Console.WriteLine(message.GetStringContent());


        Console.ReadLine();
        //await client.Direct.RequestJson<RequestModel>(new RequestModel());
        await client.ConnectAsync();

        Console.ReadLine();

        //var r = await client.Queue.Create("test", new List<KeyValuePair<string, string>> { new(HorseHeaders.QUEUE_TYPE, "push") });
        //Console.WriteLine(r);
        //ModelC c = new ModelC();

        while (true)
        {
            ModelA a = new ModelA();
            a.Foo = new string('b', 18);
            a.No = 123;

            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
                await client.Queue.Push(a, true, CancellationToken.None);

            sw.Stop();

            Console.WriteLine($"Push: in {sw.ElapsedTicks} ticks ({sw.ElapsedMilliseconds} ms)");

            // HorseResult result = await client.Router.PublishRequest<TestQuery, TestQueryResult>("test-service-route", query, 1);
            // Console.WriteLine($"Push: {result.Code}");

            //HorseResult result = await client.Router.Publish("test-service-route", command, null, true, 2);
            // Console.WriteLine($"Push: {result.Code}");

            // var result = await client.Direct.Request<ResponseModel>(new RequestModel());
            // Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");
/*
                var result = await client.Queue.Push("SampleTestEvent", new TestEvent(), true, CancellationToken.None);

                Console.WriteLine($"Push: {result.Code} {result.Reason} ${JsonSerializer.Serialize(result)}");*/
            Console.ReadLine();
            // await Task.Delay(5000);
        }
    }
}