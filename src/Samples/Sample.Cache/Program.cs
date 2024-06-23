using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Cache;
using Horse.Messaging.Server;
using Horse.Server;

namespace Sample.Cache;

class Program
{
    static async Task Main(string[] args)
    {
        HorseRider rider = StartServer();

        HorseClientBuilder builder = new HorseClientBuilder();
        builder.AddHost("horse://localhost:2626");

        HorseClient client = builder.Build();
        client.ResponseTimeout = TimeSpan.FromMinutes(30);
        client.Connected += c => Console.WriteLine("Connected");
        client.Disconnected += c => Console.WriteLine("Disconnected");

        await client.ConnectAsync();

        HorseCacheData<int> incrementalData;

        while (true)
        {
            try
            {
                Console.ReadLine();
                incrementalData = await client.Cache.GetIncrementalValue("IncrementalTestKey");
                Console.WriteLine($"Incremental value: {incrementalData.Value}");
            }
            catch { }
        }
        
        //client.Cache.Set<CacheModel>("modelA", null);
        // client.Cache.Get<CacheModel>();

        Console.ReadLine();
    }

    private static HorseRider StartServer()
    {
        HorseRider rider = HorseRiderBuilder.Create().Build();

        HorseServer server = new HorseServer();
        server.UseRider(rider);
        server.Start(26223);
        return rider;
    }
}