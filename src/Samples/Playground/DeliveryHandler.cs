using System;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Data;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Playground
{
    public class DeliveryHandler : IMessageDeliveryHandler
    {
        public long Count;

        private Database _database;

        public DeliveryHandler(int count)
        {
            _database = new Database(new DatabaseOptions
                                     {
                                         Filename = "/home/mehmet/Desktop/tdb/t" + count + ".tdb",
                                         AutoFlush = true,
                                         FlushInterval = TimeSpan.FromSeconds(5),
                                         AutoShrink = true,
                                         InstantFlush = false,
                                         ShrinkInterval = TimeSpan.FromMinutes(60),
                                         CreateBackupOnShrink = true
                                     });
        }

        public async Task Init()
        {
            await _database.Open();
        }

        public Task<Decision> ReceivedFromProducer(TwinoQueue queue, QueueMessage message, MqClient sender)
        {
            Interlocked.Increment(ref Count);
            return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.None));
        }

        /*
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            Interlocked.Increment(ref Count);
            await _database.Insert(message.Message);
            return Decision.JustAllow();
        }*/

        public Task<Decision> BeginSend(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> CanConsumerReceive(TwinoQueue queue, QueueMessage message, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> ConsumerReceived(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> ConsumerReceiveFailed(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> EndSend(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /*
        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            await _database.Delete(message.Message);
            return Decision.JustAllow();
        }*/

        public Task<Decision> AcknowledgeReceived(TwinoQueue queue, TwinoMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            //   await _database.Delete(delivery.Message.Message);
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> MessageTimedOut(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> AcknowledgeTimedOut(TwinoQueue queue, MessageDelivery delivery)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task MessageDequeued(TwinoQueue queue, QueueMessage message)
        {
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(TwinoQueue queue, QueueMessage message, Exception exception)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<bool> SaveMessage(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(false); // _database.Insert(message.Message);
        }
    }
}