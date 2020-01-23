using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class RoundRobinQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private static readonly TmqWriter _writer = new TmqWriter();
        private readonly ChannelQueue _queue;

        /// <summary>
        /// Round robin client list index
        /// </summary>
        private int _roundRobinIndex = -1;

        public RoundRobinQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        /// <summary>
        /// Adds the message to the queue and pulls first message from the queue.
        /// Usually first message equals message itself.
        /// But sometimes, previous messages might be pending in the queue.
        /// </summary>
        private QueueMessage GetFirstMessageFromQueue(QueueMessage message)
        {
            QueueMessage held;
            if (message.Message.HighPriority)
            {
                lock (_queue.HighPriorityLinkedList)
                {
                    //we don't need push and pull
                    if (_queue.HighPriorityLinkedList.Count == 0)
                    {
                        message.IsInQueue = false;
                        return message;
                    }

                    _queue.HighPriorityLinkedList.AddLast(message);
                    message.IsInQueue = true;
                    held = _queue.HighPriorityLinkedList.First.Value;
                    _queue.HighPriorityLinkedList.RemoveFirst();
                    _queue.Info.UpdateHighPriorityMessageCount(_queue.HighPriorityLinkedList.Count);
                }
            }
            else
            {
                lock (_queue.RegularLinkedList)
                {
                    //we don't need push and pull
                    if (_queue.RegularLinkedList.Count == 0)
                    {
                        message.IsInQueue = false;
                        return message;
                    }

                    _queue.RegularLinkedList.AddLast(message);
                    message.IsInQueue = true;
                    held = _queue.RegularLinkedList.First.Value;
                    _queue.RegularLinkedList.RemoveFirst();
                    _queue.Info.UpdateRegularMessageCount(_queue.RegularLinkedList.Count);
                }
            }

            if (held != null)
                held.IsInQueue = false;

            return held;
        }

        public async Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            QueueMessage held = GetFirstMessageFromQueue(message);
            ChannelClient cc = _queue.Channel.GetNextRRClient(ref _roundRobinIndex);
            if (cc == null)
            {
                _queue.AddMessage(held, false);
                return PushResult.NoConsumers;
            }

            ProcessingMessage = held;
            PushResult result = await ProcessMessage(held, cc);
            ProcessingMessage = null;

            await _queue.Trigger();

            return result;
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message, ChannelClient receiver)
        {
            //if we need acknowledge from receiver, it has a deadline.
            DateTime? deadline = null;
            if (_queue.Options.RequestAcknowledge)
                deadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //if to process next message is requires previous message acknowledge, wait here
            if (_queue.Options.RequestAcknowledge && _queue.Options.WaitForAcknowledge)
                await _queue.WaitForAcknowledge(message);

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create prepared message data
            byte[] messageData = await _writer.Create(message.Message);

            //call before send and check decision
            message.Decision = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, receiver.Client);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, receiver, deadline);
            delivery.FirstAcquirer = message.Message.FirstAcquirer;

            //send the message
            bool sent = receiver.Client.Send(messageData);

            if (sent)
            {
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

            if (message.Decision.Allow && !message.Decision.KeepMessage)
            {
                _queue.Info.AddMessageRemove();
                _ = _queue.DeliveryHandler.MessageRemoved(_queue, message);
            }

            return PushResult.Success;
        }

        public async Task Trigger()
        {
            if (_queue.HighPriorityLinkedList.Count > 0)
                await ProcessPendingMessages(_queue.HighPriorityLinkedList);

            if (_queue.RegularLinkedList.Count > 0)
                await ProcessPendingMessages(_queue.RegularLinkedList);
        }

        /// <summary>
        /// Start to process all pending messages.
        /// This method is called after a client is subscribed to the queue.
        /// </summary>
        private async Task ProcessPendingMessages(LinkedList<QueueMessage> list)
        {
            int max = list.Count;
            for (int i = 0; i < max; i++)
            {
                QueueMessage message;
                lock (list)
                {
                    if (list.Count == 0)
                        return;

                    message = list.First.Value;
                    list.RemoveFirst();
                    message.IsInQueue = false;
                }

                try
                {
                    ChannelClient cc = _queue.Channel.GetNextRRClient(ref _roundRobinIndex);
                    if (cc == null)
                    {
                        _queue.AddMessage(message, false);
                        break;
                    }

                    ProcessingMessage = message;
                    await ProcessMessage(message, cc);
                    ProcessingMessage = null;
                }
                catch (Exception ex)
                {
                    _queue.Info.AddError();
                    try
                    {
                        Decision decision = await _queue.DeliveryHandler.ExceptionThrown(_queue, message, ex);
                        await _queue.ApplyDecision(decision, message);
                    }
                    catch //if developer does wrong operation, we should not stop
                    {
                    }
                }
            }
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