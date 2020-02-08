using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Data
{
    public class ShrinkTest
    {
        private volatile int _totalInsert;
        private volatile int _failedInsert;
        private volatile int _totalDelete;

        private volatile bool _running;

        private List<string> _idList = new List<string>();
        private DefaultUniqueIdGenerator _generator = new DefaultUniqueIdGenerator();

        [Fact]
        public async Task OverloadAsync()
        {
            Database db = new Database(new DatabaseOptions
                                       {
                                           Filename = "/home/mehmet/Desktop/tdb/test.tdb",
                                           AutoShrink = false,
                                           ShrinkInterval = TimeSpan.FromSeconds(5),
                                           AutoFlush = true,
                                           FlushInterval = TimeSpan.FromSeconds(1),
                                           CreateBackupOnShrink = true
                                       });

            await db.Open();
            _running = true;
            _totalDelete = 0;
            _totalInsert = 0;
            _failedInsert = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<Thread> threads = new List<Thread>();
            for (int i = 0; i < 100; i++)
            {
                Thread thread = new Thread(async () =>
                {
                    Random rnd = new Random();
                    while (_running)
                    {
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

                                //delete
                            }
                        }
                        catch
                        {
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Start();
                threads.Add(thread);
            }

            SpinWait spin = new SpinWait();
            while (sw.ElapsedMilliseconds < 60000)
                spin.SpinOnce();

            _running = false;
            Thread.Sleep(2000);

            int left = _totalInsert - _totalDelete;
            var items = await db.List();
            Assert.Equal(left, items.Count);

            await db.Close();
            await db.Open();
            Assert.Equal(left, items.Count);
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