using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.States
{
    internal class PushQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => true;

        private readonly HorseQueue _queue;

        public PushQueueState(HorseQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(QueueClient client, HorseMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public async Task<PushResult> Push(QueueMessage message)
        {
            try
            {
                if (!message.Deadline.HasValue && _queue.Options.MessageTimeout > TimeSpan.Zero)
                    message.Deadline = DateTime.UtcNow.Add(_queue.Options.MessageTimeout);

                ProcessingMessage = message;
                PushResult result = await ProcessMessage(message);
                return result;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("PUSH", e, $"QueueName:{_queue.Name}, State:Push");
                return PushResult.Error;
            }
            finally
            {
                ProcessingMessage = null;
            }
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message)
        {
            //if we need acknowledge from receiver, it has a deadline.
            DateTime? ackDeadline = null;
            if (_queue.Options.Acknowledge != QueueAckDecision.None)
                ackDeadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //if to process next message is requires previous message acknowledge, wait here
            if (_queue.Options.Acknowledge == QueueAckDecision.WaitForAcknowledge)
                await _queue.WaitForAcknowledge(message);

            //if there are not receivers, complete send operation
            List<QueueClient> clients = _queue.ClientsClone;
            if (clients.Count == 0)
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
            byte[] messageData = HorseProtocolWriter.Create(message.Message);

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
                final = HorseQueue.CreateFinalDecision(final, ccrd);

                if (!ccrd.Allow)
                    continue;

                //create delivery object
                MessageDelivery delivery = new MessageDelivery(message, client, ackDeadline);

                //send the message
                bool sent = await client.Client.SendAsync(messageData);

                if (sent)
                {
                    if (_queue.Options.Acknowledge != QueueAckDecision.None)
                    {
                        client.CurrentlyProcessing = message;
                        client.ProcessDeadline = ackDeadline ?? DateTime.UtcNow;
                    }

                    messageIsSent = true;
                    message.CurrentDeliveryReceivers.Add(client);

                    //adds the delivery to time keeper to check timing up
                    _queue.TimeKeeper.AddAcknowledgeCheck(delivery);

                    //mark message is sent
                    delivery.MarkAsSent();

                    //do after send operations for per message
                    _queue.Info.AddDelivery();
                    Decision d = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, client.Client);

                    foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                        _ = handler.OnConsumed(_queue, delivery, client.Client);

                    final = HorseQueue.CreateFinalDecision(final, d);
                }
                else
                {
                    Decision d = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, client.Client);
                    final = HorseQueue.CreateFinalDecision(final, d);
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