using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Sample.Consumer;

namespace Sample.Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HorseClientBuilder builder = new HorseClientBuilder();
            builder.SetHost("horse://localhost:9999");
            builder.UseNewtonsoftJsonSerializer();
            HorseClient client = builder.Build();
            client.Connect();

            ModelA a = new ModelA();
            a.Foo = "foo";
            a.No = 123;

            while (true)
            {
                HorseResult result = await client.Queue.PushJson(a, false);
                Console.WriteLine($"Push: {result.Code}");

                // var result = await client.Direct.RequestJson<ResponseModel>(new RequestModel());
                // Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");
                Console.ReadLine();
                // await Task.Delay(5000);
            }
        }
    }
}