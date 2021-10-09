using System;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Queues.Store
{
    public class DefaultMessageTimeoutTracker : IMessageTimeoutTracker
    {
        public IQueueMessageStore Store { get; }

        private readonly HorseQueue _queue;
        private readonly Thread _checker;
        private bool _running = false;

        public DefaultMessageTimeoutTracker(HorseQueue queue, IQueueMessageStore store)
        {
            Store = store;
            _queue = queue;
            
            _checker = new Thread(async () =>
            {
                try
                {
                    Thread.Sleep(1000);
                    while (_running)
                    {
                        Thread.Sleep(1000);
                        if (_queue.Options.MessageTimeout == TimeSpan.Zero)
                        {
                            Thread.Sleep(10000);
                            continue;
                        }

                        QueueMessage message;
                        do
                        {
                            message = Store.ReadFirst();

                            if (message == null)
                                break;

                            if (!message.Deadline.HasValue)
                                break;

                            if (message.Deadline.Value > DateTime.UtcNow + _queue.Options.MessageTimeout)
                                break;

                            Store.Remove(message);
                            
                            _queue.Info.AddMessageTimeout();
                            message.MarkAsTimedOut();

                            await Store.Manager.RemoveMessage(message);

                            foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                                _ = handler.MessageTimedOut(_queue, message);

                            _queue.MessageTimeoutEvent.Trigger(new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.Message.MessageId));
                            
                            message = Store.ReadFirst();

                        } while (message != null && message.Deadline.HasValue);
                    }
                }
                catch
                {
                    //todo: log
                }
            });
        }

        public void Start()
        {
            _running = true;
            _checker.Start();
        }

        public void Stop()
        {
            _running = false;
        }
    }
}