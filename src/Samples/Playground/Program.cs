using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Handlers;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit.Sdk;

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
            foreach (KeyValuePair<string,TmqMessage> v in messages)
            {
                Console.WriteLine(v.Value.ToString());
            }
            
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