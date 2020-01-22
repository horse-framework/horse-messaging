using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues.States
{
    internal class RouteQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }

        private static readonly TmqWriter _writer = new TmqWriter();
        private readonly ChannelQueue _queue;

        public RouteQueueState(ChannelQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(ChannelClient client, TmqMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public async Task<PushResult> Push(QueueMessage message, MqClient sender)
        {
            ProcessingMessage = message;
            PushResult result = await ProcessMessage(message, sender);
            ProcessingMessage = null;

            await Trigger();

            return result;
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message, MqClient sender)
        {
            //if we need acknowledge from receiver, it has a deadline.
            DateTime? ackDeadline = null;
            if (_queue.Options.RequestAcknowledge)
                ackDeadline = DateTime.UtcNow.Add(_queue.Options.AcknowledgeTimeout);

            //if there are not receivers, complete send operation
            List<ChannelClient> clients = _queue.Channel.ClientsClone;
            if (clients.Count == 0)
            {
                _queue.Info.AddMessageRemove();
                _ = _queue.DeliveryHandler.MessageRemoved(_queue, message);

                return PushResult.NoConsumers;
            }

            //if to process next message is requires previous message acknowledge, wait here
            if (_queue.Options.RequestAcknowledge && _queue.Options.WaitForAcknowledge)
                await _queue.WaitForAcknowledge(message);

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create prepared message data
            byte[] messageData = await _writer.Create(message.Message);

            Decision final = new Decision(false, false, false, DeliveryAcknowledgeDecision.None);
            bool messageIsSent = false;

            //to all receivers
            foreach (ChannelClient client in clients)
            {
                //to only online receivers
                if (!client.Client.IsConnected)
                    continue;

                //somehow if code comes here (it should not cuz of last "break" in this foreach, break
                if (!message.Message.FirstAcquirer && _queue.Options.SendOnlyFirstAcquirer)
                    break;

                //call before send and check decision
                Decision ccrd = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, client.Client);
                final = ChannelQueue.CreateFinalDecision(final, ccrd);

                if (!ccrd.Allow)
                    continue;

                //create delivery object
                MessageDelivery delivery = new MessageDelivery(message, client, ackDeadline);
                delivery.FirstAcquirer = message.Message.FirstAcquirer;

                //send the message
                bool sent = client.Client.Send(messageData);

                if (sent)
                {
                    messageIsSent = true;

                    //adds the delivery to time keeper to check timing up
                    _queue.TimeKeeper.AddAcknowledgeCheck(delivery);

                    //set as sent, if message is sent to it's first acquirer,
                    //set message first acquirer false and re-create byte array data of the message
                    bool firstAcquirer = message.Message.FirstAcquirer;

                    //mark message is sent
                    delivery.MarkAsSent();

                    //do after send operations for per message
                    _queue.Info.AddDelivery();
                    Decision d = await _queue.DeliveryHandler.ConsumerReceived(_queue, delivery, client.Client);
                    final = ChannelQueue.CreateFinalDecision(final, d);

                    //if we are sending to only first acquirer, break
                    if (_queue.Options.SendOnlyFirstAcquirer && firstAcquirer)
                        break;

                    if (firstAcquirer && clients.Count > 1)
                        messageData = await _writer.Create(message.Message);
                }
                else
                {
                    Decision d = await _queue.DeliveryHandler.ConsumerReceiveFailed(_queue, delivery, client.Client);
                    final = ChannelQueue.CreateFinalDecision(final, d);
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

        public Task<QueueStatusAction> EnterStatus(QueueStatus previousStatus)
        {
            return Task.FromResult(QueueStatusAction.AllowAndTrigger);
        }

        public Task<QueueStatusAction> LeaveStatus(QueueStatus nextStatus)
        {
            return Task.FromResult(QueueStatusAction.Allow);
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
                    PushResult pr = await Push(message, null);
                    if (pr == PushResult.Empty || pr == PushResult.NoConsumers)
                        return;
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
    }
}