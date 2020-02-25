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
        public bool TriggerSupported => true;

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
            ChannelClient cc = _queue.Channel.GetNextRRClient(ref _roundRobinIndex);
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