using System.Threading.Tasks;
using Horse.Messaging.Client;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HorseClient client = new HorseClient();


            using (HorseTransaction transaction = new HorseTransaction(client, "TransactionName"))
            {
                await transaction.Begin();
                //todo: do something
                bool commited = await transaction.Commit();
            }

            using (HorseTransaction transaction = await HorseTransaction.Begin(client, "Name"))
            {
                //todo: do something
                bool success = true;

                if (success)
                {
                    bool commited = await transaction.Commit();
                }
            }
        }
    }
}