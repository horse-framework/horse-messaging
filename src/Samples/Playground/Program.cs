using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Database database = new Database(new DatabaseOptions
                                             {
                                                 Filename = "/home/mehmet/Desktop/test.tdb",
                                                 AutoFlush = false,
                                                 AutoShrink = false,
                                                 InstantFlush = true,
                                                 ShrinkInterval = TimeSpan.FromSeconds(30),
                                                 FlushInterval = TimeSpan.FromSeconds(5),
                                                 CreateBackupOnShrink = true
                                             });

            await database.Open();
            var messages = await database.List();
            Console.WriteLine($"There are {messages.Count} messages in database");
            int x = 0;
            List<TmqMessage> remove = new List<TmqMessage>();
            foreach (KeyValuePair<string,TmqMessage> v in messages)
            {
                Console.WriteLine(v.Value.ToString());
                x++;
                if (x == 3 || x == 4 || x == 6)
                    remove.Add(v.Value);
            }

            foreach (TmqMessage tmqMessage in remove)
                await database.Delete(tmqMessage);

            await database.Shrink();
            
            DefaultUniqueIdGenerator generator = new DefaultUniqueIdGenerator();
            while (true)
            {
                Console.ReadLine();
                TmqMessage message = new TmqMessage(MessageType.Channel, "channel");
                message.SetMessageId(generator.Create());
                message.SetStringContent("Hello, World!");

                bool saved = await database.Insert(message);
                Console.WriteLine(saved ? "Saved" : "Failed");
            }
        }
    }
}