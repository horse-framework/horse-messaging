using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class PullQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private readonly ChannelQueue _queue;

        public PullQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public async Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            QueueMessage message = null;

            //pull from prefential messages
            if (_queue.HighPriorityLinkedList.Count > 0)
                lock (_queue.HighPriorityLinkedList)
                {
                    message = _queue.HighPriorityLinkedList.First.Value;
                    _queue.HighPriorityLinkedList.RemoveFirst();

                    if (message != null)
                        message.IsInQueue = false;
                }

            //if there is no prefential message, pull from standard messages
            if (message == null && _queue.RegularLinkedList.Count > 0)
            {
                lock (_queue.RegularLinkedList)
                {
                    message = _queue.RegularLinkedList.First.Value;
                    _queue.RegularLinkedList.RemoveFirst();

                    if (message != null)
                        message.IsInQueue = false;
                }
            }

            //there is no pullable message
            if (message == null)
            {
                await client.Client.SendAsync(MessageBuilder.ResponseStatus(request, KnownContentTypes.NotFound));
                return PullResult.Empty;
            }

            try
            {
                await ProcessPull(client, request, message);
            }
            catch (Exception ex)
            {
                _queue.Info.AddError();
                try
                {
                    Decision decision = await _queue.DeliveryHandler.ExceptionThrown(_queue, message, ex);
                    await _queue.ApplyDecision(decision, message);

                    if (decision.KeepMessage && !message.IsInQueue)
                        _queue.AddMessage(message, false);
                }
                catch //if developer does wrong operation, we should not stop
                {
                }
            }

            return PullResult.Success;
        }

        /// <summary>
        /// Process pull request and sends queue message to requester as response
        /// </summary>
        private async Task ProcessPull(ChannelClient requester, TmqMessage request, QueueMessage message)
        {
            //if we need acknowledge, we are sending this information to receivers that we require response
            message.Message.AcknowledgeRequired = _queue.Options.RequestAcknowledge;

            //if we need acknowledge from receiver, it has a deadline.
            DateTime? deadline = null;
            if (_queue.Options.RequestAcknowledge)
                deadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //if to process next message is requires previous message acknowledge, wait here
            if (_queue.Options.RequestAcknowledge && _queue.Options.WaitForAcknowledge)
                await _queue.WaitForAcknowledge(message);

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return;

            //if message is sent before and this is second client, skip the process
            bool skip = !message.Message.FirstAcquirer && _queue.Options.SendOnlyFirstAcquirer;
            if (skip)
            {
                if (!message.Decision.KeepMessage)
                {
                    _queue.Info.AddMessageRemove();
                    _ = _queue.DeliveryHandler.MessageRemoved(_queue, message);
                }

                return;
            }

            //call before send and check decision
            message.Decision = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, requester.Client);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return;

            //create delivery object
            MessageDelivery delivery = new MessageDelivery(message, requester, deadline);
            delivery.FirstAcquirer = message.Message.FirstAcquirer;

            //change to response message, send, change back to channel message
            string mid = message.Message.MessageId;
            message.Message.SetMessageId(request.MessageId);
            message.Message.Type = MessageType.Response;

            bool sent = requester.Client.Send(message.Message);
            message.Message.SetMessageId(mid);
            message.Message.Type = MessageType.Channel;

            if (sent)
            {
                _queue.TimeKeeper.AddAcknowledgeCheck(delivery);
                delivery.MarkAsSent();

                //do after send operations for per message
                _queue.Info.AddDelivery();
                message.Decision = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, requester.Client);

                //after all sending operations completed, calls implementation send completed method and complete the operation
                _queue.Info.AddMessageSend();

                if (!await _queue.ApplyDecision(message.Decision, message))
                    return;
            }
            else
            {
                message.Decision = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, requester.Client);
                if (!await _queue.ApplyDecision(message.Decision, message))
                    return;
            }

            message.Decision = await _queue.DeliveryHandler.EndSend(_queue, message);
            await _queue.ApplyDecision(message.Decision, message);

            if (message.Decision.Allow && !message.Decision.KeepMessage)
            {
                _queue.Info.AddMessageRemove();
                _ = _queue.DeliveryHandler.MessageRemoved(_queue, message);
            }
        }

        public Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            _queue.AddMessage(message);
            return Task.FromResult(PushResult.Success);
        }

        public Task Trigger()
        {
            return Task.CompletedTask;
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