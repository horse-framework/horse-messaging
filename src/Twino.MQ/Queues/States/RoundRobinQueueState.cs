using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class RoundRobinQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => true;

        private readonly TwinoQueue _queue;

        /// <summary>
        /// Round robin client list index
        /// </summary>
        private int _roundRobinIndex = -1;

        public RoundRobinQueueState(TwinoQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(QueueClient client, TwinoMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public bool CanEnqueue(QueueMessage message)
        {
            //if we have an option maximum wait duration for message, set it after message joined to the queue.
            //time keeper will check this value and if message time is up, it will remove message from the queue.
            if (_queue.Options.MessageTimeout > TimeSpan.Zero)
                message.Deadline = DateTime.UtcNow.Add(_queue.Options.MessageTimeout);

            return true;
        }

        public async Task<PushResult> Push(QueueMessage message)
        {
            try
            {
                QueueClient cc;
                if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                {
                    Tuple<QueueClient, int> tuple = await _queue.GetNextAvailableRRClient(_roundRobinIndex);
                    cc = tuple.Item1;
                    if (cc != null)
                        _roundRobinIndex = tuple.Item2;
                }
                else
                    cc = _queue.GetNextRRClient(ref _roundRobinIndex);

                /*
                //if to process next message is requires previous message acknowledge, wait here
                if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                    await _queue.WaitForAcknowledge(message);
                */

                if (cc == null)
                {
                    _queue.AddMessage(message, false);
                    return PushResult.NoConsumers;
                }

                ProcessingMessage = message;
                PushResult result = await ProcessMessage(message, cc);
                ProcessingMessage = null;

                return result;
            }
            catch (Exception e)
            {
                _queue.Server.SendError("PUSH", e, $"QueueName:{_queue.Name}, State:RoundRobin");
                return PushResult.Error;
            }
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message, QueueClient receiver)
        {
            //if we need acknowledge from receiver, it has a deadline.
            DateTime? deadline = null;
            if (_queue.Options.Acknowledge != QueueAckDecision.None)
                deadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //return if client unsubsribes while waiting ack of previous message
            if (!_queue.ClientsClone.Contains(receiver))
            {
                _queue.AddMessage(message, false);
                return PushResult.NoConsumers;
            }
            
            if (message.CurrentDeliveryReceivers.Count > 0)
                message.CurrentDeliveryReceivers.Clear();

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create prepared message data
            byte[] messageData = TmqWriter.Create(message.Message);

            //call before send and check decision
            message.Decision = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, receiver.Client);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, receiver, deadline);

            //send the message
            bool sent = await receiver.Client.SendAsync(messageData);

            if (sent)
            {
                if (_queue.Options.Acknowledge != QueueAckDecision.None)
                {
                    receiver.CurrentlyProcessing = message;
                    receiver.ProcessDeadline = deadline ?? DateTime.UtcNow;
                }
                
                message.CurrentDeliveryReceivers.Add(receiver);

                //adds the delivery to time keeper to check timing up
                _queue.TimeKeeper.AddAcknowledgeCheck(delivery);

                //mark message is sent
                delivery.MarkAsSent();
                _queue.Info.AddMessageSend();

                //do after send operations for per message
                _queue.Info.AddDelivery();
                message.Decision = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, receiver.Client);
            }
            else
                message.Decision = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, receiver.Client);

            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            message.Decision = await _queue.DeliveryHandler.EndSend(_queue, message);
            await _queue.ApplyDecision(message.Decision, message);

            if (message.Decision.Allow && message.Decision.PutBack == PutBackDecision.No)
            {
                _queue.Info.AddMessageRemove();
                _ = _queue.DeliveryHandler.MessageDequeued(_queue, message);
            }

            return PushResult.Success;
        }

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            return Task.FromResult(QueueStatusAction.AllowAndTrigger);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
        }
    }
}