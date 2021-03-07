using System;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Delivery;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Queues.States
{
    internal class CacheQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => false;

        private readonly HorseQueue _queue;

        public CacheQueueState(HorseQueue queue)
        {
            _queue = queue;
        }

        public async Task<PullResult> Pull(QueueClient client, HorseMessage request)
        {
            QueueMessage message = _queue.FindNextMessage();
            if (message == null)
            {
                HorseMessage eoq = new HorseMessage(MessageType.QueueMessage, _queue.Name);
                eoq.SetMessageId(request.MessageId);
                eoq.AddHeader(HorseHeaders.REQUEST_ID, request.MessageId);
                eoq.AddHeader(HorseHeaders.NO_CONTENT, HorseHeaders.END);
                await client.Client.SendAsync(eoq);
                return PullResult.Empty;
            }

            if (message.CurrentDeliveryReceivers.Count > 0)
                message.CurrentDeliveryReceivers.Clear();

            ProcessingMessage = message;

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!message.Decision.Allow)
                return PullResult.Success;

            //call before send and check decision
            message.Decision = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, client.Client);
            if (!message.Decision.Allow)
                return PullResult.Success;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, client);

            //change to response message, send, change back to queue message
            message.Message.SetMessageId(request.MessageId);
            message.Message.SetOrAddHeader(HorseHeaders.REQUEST_ID, request.MessageId);
            message.Message.AddHeader(HorseHeaders.NO_CONTENT, HorseHeaders.END);

            bool sent = await client.Client.SendAsync(message.Message);

            if (sent)
            {
                delivery.MarkAsSent();

                //do after send operations for per message
                _queue.Info.AddDelivery();
                message.Decision = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, client.Client);

                //after all sending operations completed, calls implementation send completed method and complete the operation
                _queue.Info.AddMessageSend();
            }
            else
                await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, client.Client);

            return PullResult.Success;
        }

        public bool CanEnqueue(QueueMessage message)
        {
            //if we need acknowledge, we are sending this information to receivers that we require response
            message.Message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
            
            if (_queue.Options.MessageTimeout > TimeSpan.Zero)
                message.Deadline = DateTime.UtcNow.Add(_queue.Options.MessageTimeout);

            _queue.ClearAllMessages();
            _queue.AddMessage(message);
            return true;
        }

        public Task<PushResult> Push(QueueMessage message)
        {
            return Task.FromResult(PushResult.Success);
        }

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }
    }
}