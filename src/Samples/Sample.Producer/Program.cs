using System;
using System.Threading.Tasks;
using Sample.Producer.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;

namespace Sample.Producer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TmqClient client = new TmqClient();
            client.AcknowledgeTimeout = TimeSpan.FromSeconds(3);
            await client.ConnectAsync("tmq://127.0.0.1:22200");

            while (true)
            {
                ModelA a = new ModelA();
                a.Name = "Model A";
                a.No = 1;
                
                TwinoResult resultA = await client.Queues.PushJson(a, true);
                
                await Task.Delay(500);
                
                ModelB b = new ModelB();
                b.FirstName = "Mehmet";
                b.LastName = "Helvacikoylu";

                TwinoResult resultB = await client.Queues.PushJson(b, false);
                
                await Task.Delay(500);

                ModelC c = new ModelC();
                c.Value = "Hello";
                await client.SendJsonAsync(MessageType.DirectMessage, c, false);

                await Task.Delay(500);
            }
        }
    }
}