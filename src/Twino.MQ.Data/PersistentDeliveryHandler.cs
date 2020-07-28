using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public enum DeleteWhen
    {
        AfterSend,
        AfterAcknowledgeReceived
    }

    public class PersistentDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly ChannelQueue _queue;
        private readonly DeleteWhen _deleteWhen;
        private readonly DeliveryAcknowledgeDecision _acknowledgeDecision;
        private readonly Database _database;
        private readonly Action<ChannelQueue, QueueMessage, Exception> _exception;

        #region Init - Destroy

        public PersistentDeliveryHandler(ChannelQueue queue,
                                         DatabaseOptions options,
                                         DeleteWhen deleteWhen,
                                         DeliveryAcknowledgeDecision acknowledgeDecision,
                                         Action<ChannelQueue, QueueMessage, Exception> exception)
        {
            _queue = queue;
            _deleteWhen = deleteWhen;
            _acknowledgeDecision = acknowledgeDecision;
            _exception = exception;
            _database = new Database(options);
        }

        public async Task Initialize()
        {
            await _database.Open();
            var dict = await _database.List();
            if (dict.Count > 0)
            {
                QueueFiller filler = new QueueFiller(_queue);
                PushResult result = filler.FillMessage(dict.Values, true);
                if (result != PushResult.Success)
                    throw new InvalidOperationException($"Cannot fill messages into {_queue.Id} queue in {_queue.Channel.Name} : {result}");
            }
        }

        internal void Destroy(ChannelQueue queue)
        {
            //todo: subscribe deleted event to remove from options
            throw new NotImplementedException();
        }

        #endregion

        #region Events

        public Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            return Task.FromResult(new Decision(true, true, PutBackDecision.No, _acknowledgeDecision));
        }

        public Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, true, PutBackDecision.No, _acknowledgeDecision));
        }

        public Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return new Decision(true, true, PutBackDecision.Start, _acknowledgeDecision);

            if (_deleteWhen == DeleteWhen.AfterSend)
                await DeleteMessage(message.Message.MessageId);

            return Decision.JustAllow();
        }

        public async Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (_deleteWhen == DeleteWhen.AfterAcknowledgeReceived)
                await DeleteMessage(delivery.Message.Message.MessageId);

            return Decision.JustAllow();
        }

        public async Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            await DeleteMessage(message.Message.MessageId);
            return Decision.JustAllow();
        }

        private async Task DeleteMessage(string id)
        {
            for (int i = 0; i < 3; i++)
            {
                bool deleted = await _database.Delete(id);
                if (deleted)
                    return;

                await Task.Delay(3);
            }
        }

        public Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            return Task.FromResult(new Decision(true, true, PutBackDecision.Start, _acknowledgeDecision));
        }

        public Task MessageDequeued(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        public Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            if (_exception != null)
                _exception(queue, message, exception);

            return Task.FromResult(Decision.JustAllow());
        }

        public Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            return _database.Insert(message.Message);
        }

        #endregion
    }
}