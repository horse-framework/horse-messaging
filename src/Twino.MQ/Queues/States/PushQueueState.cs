using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class PushQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => true;

        private readonly TwinoQueue _queue;

        public PushQueueState(TwinoQueue queue)
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
                ProcessingMessage = message;
                PushResult result = await ProcessMessage(message);
                ProcessingMessage = null;
                return result;
            }
            catch (Exception e)
            {
                _queue.Server.SendError("PUSH", e, $"QueueName:{_queue.Name}, State:Push");
                return PushResult.Error;
            }
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message)
        {
            //if we need acknowledge from receiver, it has a deadline.
            DateTime? ackDeadline = null;
            if (_queue.Options.Acknowledge != QueueAckDecision.None)
                ackDeadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //if there are not receivers, complete send operation
            List<QueueClient> clients = _queue.ClientsClone;
            if (clients.Count == 0)
            {
                _queue.AddMessage(message, false);
                return PushResult.NoConsumers;
            }

            //if to process next message is requires previous message acknowledge, wait here
            if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                await _queue.WaitForAcknowledge(message);

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create prepared message data
            byte[] messageData = TmqWriter.Create(message.Message);

            Decision final = new Decision(false, false, PutBackDecision.No, DeliveryAcknowledgeDecision.None);
            bool messageIsSent = false;

            //to all receivers
            foreach (QueueClient client in clients)
            {
                //to only online receivers
                if (!client.Client.IsConnected)
                    continue;

                //call before send and check decision
                Decision ccrd = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, client.Client);
                final = TwinoQueue.CreateFinalDecision(final, ccrd);

                if (!ccrd.Allow)
                    continue;

                //create delivery object
                MessageDelivery delivery = new MessageDelivery(message, client, ackDeadline);

                //send the message
                bool sent = await client.Client.SendAsync(messageData);

                if (sent)
                {
                    messageIsSent = true;

                    //adds the delivery to time keeper to check timing up
                    _queue.TimeKeeper.AddAcknowledgeCheck(delivery);

                    //mark message is sent
                    delivery.MarkAsSent();

                    //do after send operations for per message
                    _queue.Info.AddDelivery();
                    Decision d = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, client.Client);
                    final = TwinoQueue.CreateFinalDecision(final, d);
                }
                else
                {
                    Decision d = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, client.Client);
                    final = TwinoQueue.CreateFinalDecision(final, d);
                }
            }

            message.Decision = final;
            if (!await _queue.ApplyDecision(final, message))
                return PushResult.Success;

            //after all sending operations completed, calls implementation send completed method and complete the operation
            if (messageIsSent)
                _queue.Info.AddMessageSend();

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