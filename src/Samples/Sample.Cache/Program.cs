using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Server;
using Horse.Server;

namespace Sample.Cache
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HorseRider rider = StartServer();

            HorseClientBuilder builder = new HorseClientBuilder();
            builder.SetHost("horse://localhost:26223");
            HorseClient client = builder.Build();

            client.Connect();
            
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
}