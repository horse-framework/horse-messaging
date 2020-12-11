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
                await client.Client.SendAsync(request.CreateResponse(HorseResultCode.NotFound));
                return PullResult.Empty;
            }

            if (message.CurrentDeliveryReceivers.Count > 0)
                message.CurrentDeliveryReceivers.Clear();

            ProcessingMessage = message;

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PullResult.Success;

            //call before send and check decision
            message.Decision = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, client.Client);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PullResult.Success;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, client);

            //change to response message, send, change back to queue message
            message.Message.SetMessageId(request.MessageId);
            bool sent = await client.Client.SendAsync(message.Message);

            if (sent)
            {
                delivery.MarkAsSent();

                //do after send operations for per message
                _queue.Info.AddDelivery();
                message.Decision = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, client.Client);

                //after all sending operations completed, calls implementation send completed method and complete the operation
                _queue.Info.AddMessageSend();

                if (!await _queue.ApplyDecision(message.Decision, message))
                    return PullResult.Success;
            }
            else
            {
                message.Decision = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, client.Client);
                if (!await _queue.ApplyDecision(message.Decision, message))
                    return PullResult.Success;
            }

            message.Decision = await _queue.DeliveryHandler.EndSend(_queue, message);
            await _queue.ApplyDecision(message.Decision, message);

            return PullResult.Success;
        }

        public bool CanEnqueue(QueueMessage message)
        {
            //if we need acknowledge, we are sending this information to receivers that we require response
            message.Message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
            message.IsInQueue = true;

            _queue.ClearAllMessages();
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