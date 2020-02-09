using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;

namespace Sample.MQ.Data
{
    public class Overload
    {
        private volatile int _totalInsert;
        private volatile int _failedInsert;
        private volatile int _totalDelete;

        private volatile bool _running;

        private List<string> _idList = new List<string>();
        private DefaultUniqueIdGenerator _generator = new DefaultUniqueIdGenerator();

        public async Task OverloadAsync()
        {
            Database db = new Database(new DatabaseOptions
                                       {
                                           Filename = "/home/mehmet/Desktop/tdb/test.tdb",
                                           AutoShrink = true,
                                           ShrinkInterval = TimeSpan.FromMilliseconds(15000),
                                           AutoFlush = true,
                                           InstantFlush = false,
                                           FlushInterval = TimeSpan.FromMilliseconds(250),
                                           CreateBackupOnShrink = true
                                       });

            Stopwatch sw1 = new Stopwatch();
            sw1.Start();
            await db.Open();
            sw1.Stop();
            Console.WriteLine($"Database loaded with {db.MessageCount()} in {sw1.ElapsedMilliseconds} ms");
            Console.ReadLine();
            
            _running = true;
            _totalDelete = 0;
            _totalInsert = 0;
            _failedInsert = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < 50; i++)
            {
                Thread thread = new Thread(async () =>
                {
                    Random rnd = new Random();
                    while (_running)
                    {
                        Thread.Sleep(rnd.Next(1, 15));
                        try
                        {
                            if (rnd.NextDouble() < 0.75)
                            {
                                TmqMessage msg = CreateMessage();

                                bool added = await db.Insert(msg);
                                if (added)
                                {
                                    _totalInsert++;
                                    lock (_idList)
                                        _idList.Add(msg.MessageId);
                                }
                                else
                                    _failedInsert++;
                            }
                            else
                            {
                                if (_idList.Count > 50)
                                {
                                    int index = rnd.Next(_idList.Count - 40);
                                    string deleteId;
                                    lock (_idList)
                                        deleteId = _idList[index];

                                    bool deleted = await db.Delete(deleteId);
                                    if (deleted)
                                    {
                                        lock (_idList)
                                            _idList.RemoveAt(index);

                                        _totalDelete++;
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Start();
            }

            SpinWait spin = new SpinWait();
            while (sw.ElapsedMilliseconds < 6000000)
                spin.SpinOnce();

            _running = false;
            Thread.Sleep(3000);

            int left = _totalInsert - _totalDelete;
            var items = await db.List();
            Console.WriteLine("left : " + left);
            Console.WriteLine("items : " + items.Count);

            await db.Close();
            Thread.Sleep(1000);
            db = new Database(new DatabaseOptions
                              {
                                  Filename = "/home/mehmet/Desktop/tdb/test.tdb",
                                  AutoShrink = false,
                                  ShrinkInterval = TimeSpan.FromMilliseconds(3500),
                                  AutoFlush = true,
                                  InstantFlush = false,
                                  FlushInterval = TimeSpan.FromMilliseconds(250),
                                  CreateBackupOnShrink = true
                              });

            Stopwatch sw2 = new Stopwatch();
            sw2.Start();
            await db.Open();
            sw2.Stop();
            Console.WriteLine("loaded with shrink in " + sw2.ElapsedMilliseconds + " ms");

            Console.WriteLine("items : " + items.Count);
            Console.ReadLine();
        }

        private TmqMessage CreateMessage()
        {
            TmqMessage msg = new TmqMessage(MessageType.Channel, "channel");
            msg.SetMessageId(_generator.Create());
            msg.SetStringContent("Hello, World!");
            return msg;
        }
    }
}