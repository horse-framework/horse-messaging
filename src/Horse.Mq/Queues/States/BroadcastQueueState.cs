using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Delivery;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Queues.States
{
    internal class BroadcastQueueState : IQueueState
    {
        public QueueMessage ProcessingMessage { get; private set; }
        public bool TriggerSupported => true;

        private readonly HorseQueue _queue;

        public BroadcastQueueState(HorseQueue queue)
        {
            _queue = queue;
        }

        public Task<PullResult> Pull(QueueClient client, HorseMessage request)
        {
            return Task.FromResult(PullResult.StatusNotSupported);
        }

        public bool CanEnqueue(QueueMessage message)
        {
            return false;
        }

        public async Task<PushResult> Push(QueueMessage message)
        {
            try
            {
                ProcessingMessage = message;
                PushResult result = await ProcessMessage(message);
                return result;
            }
            catch (Exception e)
            {
                _queue.Server.SendError("PUSH", e, $"QueueName:{_queue.Name}, State:Broadcast");
                return PushResult.Error;
            }
            finally
            {
                ProcessingMessage = null;
            }
        }

        private async Task<PushResult> ProcessMessage(QueueMessage message)
        {
            //if there are not receivers, complete send operation
            List<QueueClient> clients = _queue.ClientsClone;
            if (clients.Count == 0)
            {
                _queue.Info.AddMessageRemove();
                return PushResult.NoConsumers;
            }

            message.Decision = await _queue.DeliveryHandler.BeginSend(_queue, message);
            if (!await _queue.ApplyDecision(message.Decision, message))
                return PushResult.Success;

            //create prepared message data
            byte[] messageData = HmqWriter.Create(message.Message);

            bool messageIsSent = false;

            //to all receivers
            foreach (QueueClient client in clients)
            {
                //to only online receivers
                if (!client.Client.IsConnected)
                    continue;

                //call before send and check decision
                Decision ccrd = await _queue.DeliveryHandler.CanConsumerReceive(_queue, message, client.Client);

                if (!ccrd.Allow)
                    continue;

                //send the message
                _ = client.Client.SendAsync(messageData);

                messageIsSent = true;

                //do after send operations for per message
                _queue.Info.AddDelivery();
            }

            //after all sending operations completed, calls implementation send completed method and complete the operation
            if (messageIsSent)
                _queue.Info.AddMessageSend();

            message.Decision = await _queue.DeliveryHandler.EndSend(_queue, message);
            await _queue.ApplyDecision(message.Decision, message);

            if (message.Decision.Allow && message.Decision.PutBack == PutBackDecision.No)
                _queue.Info.AddMessageRemove();

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