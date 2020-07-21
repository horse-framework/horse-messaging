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
        private int _totalInsert;
        private int _totalDelete;

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

            db.OnShrink += (database, info) =>
            {
                if (info.Successful)
                    Console.WriteLine($"Shrink completed in {info.TotalDuration.TotalMilliseconds} ms, " +
                                      $"prp:{info.PreparationDuration.TotalMilliseconds} ms, " +
                                      $"trn:{info.TruncateDuration.TotalMilliseconds} ms, " +
                                      $"sync:{info.SyncDuration.TotalMilliseconds} ms");
                else
                    Console.WriteLine("Shrink failed: " + info.Error);
            };

            Stopwatch sw1 = new Stopwatch();
            sw1.Start();
            await db.Open();
            sw1.Stop();
            Console.WriteLine($"Database loaded with {db.MessageCount()} in {sw1.ElapsedMilliseconds} ms");
            Console.ReadLine();

            _running = true;
            _totalDelete = 0;
            _totalInsert = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < 50; i++)
            {
                Thread thread = new Thread(async () =>
                {
                    Random rnd = new Random();
                    while (_running)
                    {
                        Thread.Sleep(rnd.Next(1, 16));
                        try
                        {
                            if (rnd.NextDouble() < 0.68)
                            {
                                TmqMessage msg = CreateMessage();

                                bool added = await db.Insert(msg);
                                if (added)
                                {
                                    Interlocked.Increment(ref _totalInsert);
                                    lock (_idList)
                                        _idList.Add(msg.MessageId);
                                }
                            }
                            else
                            {
                                if (_idList.Count > 50)
                                {
                                    lock (_idList)
                                    {
                                        int index = rnd.Next(_idList.Count - 40);
                                        string deleteId = _idList[index];
                                        bool deleted = db.Delete(deleteId).Result;
                                        if (deleted)
                                        {
                                            _idList.RemoveAt(index);
                                            Interlocked.Increment(ref _totalDelete);
                                        }
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

            Console.ReadLine();

            _running = false;
            await db.Close();
            Thread.Sleep(1000);

            int left = _totalInsert - _totalDelete;
            var items = await db.List();
            Console.WriteLine("insert : " + _totalInsert);
            Console.WriteLine("delete : " + _totalDelete);
            Console.WriteLine("id count : " + _idList.Count);
            Console.WriteLine("left : " + left);
            Console.WriteLine("items : " + items.Count);

            db = new Database(new DatabaseOptions
            {
                Filename = "/home/mehmet/Desktop/tdb/test.tdb",
                AutoShrink = false,
                ShrinkInterval = TimeSpan.FromMilliseconds(3000),
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

            var nitems = await db.List();
            Console.WriteLine("items : " + nitems.Count);
            Console.ReadLine();
        }

        private TmqMessage CreateMessage()
        {
            TmqMessage msg = new TmqMessage(MessageType.QueueMessage, "channel");
            msg.SetMessageId(_generator.Create());
            msg.SetStringContent("Hello, World!");
            return msg;
        }
    }
}