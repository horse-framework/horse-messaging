using System;
using System.Linq;
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
                                                 AutoFlush = true,
                                                 AutoShrink = true,
                                                 InstantFlush = false,
                                                 ShrinkInterval = TimeSpan.FromSeconds(30),
                                                 FlushInterval = TimeSpan.FromSeconds(5),
                                                 CreateBackupOnShrink = true
                                             });

            await database.Open();
            var messages = await database.List();
            Console.WriteLine($"There are {messages.Count} messages in database");

            DefaultUniqueIdGenerator generator = new DefaultUniqueIdGenerator();
            while (true)
            {
                string command = Console.ReadLine();
                if (string.IsNullOrEmpty(command))
                    break;

                switch (command.ToLower())
                {
                    case "i":
                        for (int i = 0; i < 300; i++)
                        {
                            TmqMessage message = new TmqMessage(MessageType.Channel, "channel");
                            message.SetMessageId(generator.Create());
                            message.SetStringContent("Hello, World!");

                            bool saved = await database.Insert(message);
                            Console.WriteLine(saved ? "Saved" : "Failed");
                        }

                        break;

                    case "d":
                        for (int j = 0; j < 100; j++)
                        {
                            var list = await database.List();
                            if (list.Count > 2)
                            {
                                Random rnd = new Random();
                                int i = rnd.Next(1, list.Count - 1);
                                var first = list.Skip(i).FirstOrDefault();
                                bool deleted = await database.Delete(first.Key);
                                Console.WriteLine($"Delete {first.Value} is {deleted}");
                            }
                        }

                        break;

                    case "s":
                        ShrinkInfo shrink = await database.Shrink();
                        Console.WriteLine($"Database shrink: {shrink.Successful} in {shrink.TotalDuration.TotalMilliseconds} ms");
                        break;
                }
            }

            await database.Close();
            Console.WriteLine("Database closed");
        }
    }
}