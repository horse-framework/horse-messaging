using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("hmq://localhost:9999");

            Console.ReadLine();

            using (HorseTransaction transaction = new HorseTransaction(client, "TransactionName"))
            {
                await transaction.Begin();

                Console.ReadLine();
                //todo: do something
                bool commited = await transaction.Commit();
            }
            Console.ReadLine();

            using (HorseTransaction transaction = await HorseTransaction.Begin(client, "Name"))
            {
                //todo: do something
                bool success = true;

                if (success)
                {
                    Console.ReadLine();
                    bool commited = await transaction.Commit();
                }
            }
        }
    }
}