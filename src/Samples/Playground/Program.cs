using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Database db = new Database(new DatabaseOptions
                                       {
                                           Filename = "/home/mehmet/Desktop/tdb/playground.tdb",
                                           AutoShrink = true,
                                           ShrinkInterval = TimeSpan.FromMilliseconds(3000),
                                           AutoFlush = false,
                                           InstantFlush = true,
                                           FlushInterval = TimeSpan.FromMilliseconds(25000),
                                           CreateBackupOnShrink = true
                                       });
            await db.Open();

            while (true)
            {
                TmqMessage msg = new TmqMessage(MessageType.QueueMessage, "a");
                msg.SetMessageId(Guid.NewGuid().ToString());
                msg.SetStringContent("Hello");
                await db.Insert(msg);
                Thread.Sleep(1000);
                await db.Delete(msg);
                Thread.Sleep(10000);
            }
        }
    }
}