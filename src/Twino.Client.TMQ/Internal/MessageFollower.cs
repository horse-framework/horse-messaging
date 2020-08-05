using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    /// <summary>
    /// Message follower, follows the messages which required acknowledge or response.
    /// If their acknowledge or response messages isn't received, fires timeout actions.
    /// </summary>
    internal class MessageFollower : IDisposable
    {
        /// <summary>
        /// Sent messages
        /// </summary>
        private readonly List<MessageDescriptor> _descriptors = new List<MessageDescriptor>();

        /// <summary>
        /// Temp message descriptor list
        /// </summary>
        private readonly List<MessageDescriptor> _temp = new List<MessageDescriptor>(8);

        /// <summary>
        /// Expiration timer
        /// </summary>
        private ThreadTimer _timer;

        /// <summary>
        /// Client of the follower
        /// </summary>
        private readonly TmqClient _client;

        public MessageFollower(TmqClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Starts to follow messages
        /// </summary>
        public void Run()
        {
            _timer = new ThreadTimer(CheckExpirations, TimeSpan.FromMilliseconds(1000));
            _timer.Start(ThreadPriority.BelowNormal);
        }

        /// <summary>
        /// Stops timers of the follower and releases all resources
        /// </summary>
        public void Dispose()
        {
            _timer?.Stop();
        }

        /// <summary>
        /// Checks expired or completed messages
        /// </summary>
        private void CheckExpirations()
        {
            if (_descriptors.Count < 1)
                return;

            _temp.Clear();

            lock (_descriptors)
            {
                foreach (MessageDescriptor descriptor in _descriptors)
                {
                    if (descriptor.Completed || descriptor.Expiration < DateTime.UtcNow)
                        _temp.Add(descriptor);
                }

                if (_temp.Count > 0)
                    _descriptors.RemoveAll(x => _temp.Contains(x));
            }

            foreach (MessageDescriptor descriptor in _temp)
            {
                if (descriptor.Completed)
                    continue;

                descriptor.Set(false, new TwinoResult(TwinoResultCode.Failed, "timeout"));
            }
        }

        /// <summary>
        /// Marks all messages as expired
        /// </summary>
        internal void MarkAllMessagesExpired()
        {
            List<MessageDescriptor> temp;
            lock (_descriptors)
                temp = new List<MessageDescriptor>(_descriptors);

            foreach (MessageDescriptor descriptor in temp)
                descriptor.Set(false, new TwinoResult(TwinoResultCode.Failed, "timeout"));
        }

        /// <summary>
        /// This method process the ack message, when it is received
        /// </summary>
        public void ProcessAcknowledge(TmqMessage message)
        {
            if (message.Type != MessageType.Acknowledge || string.IsNullOrEmpty(message.MessageId))
                return;

            MessageDescriptor descriptor;
            lock (_descriptors)
                descriptor = _descriptors.Find(x => x.Message.PendingAcknowledge && x.Message.MessageId == message.MessageId);

            if (descriptor == null)
                return;

            descriptor.Completed = true;
            if (!message.HasHeader || !message.Headers.Any(x => x.Key.Equals(TmqHeaders.NEGATIVE_ACKNOWLEDGE_REASON, StringComparison.InvariantCultureIgnoreCase)))
            {
                descriptor.Set(true, TwinoResult.Ok());
                return;
            }

            var nackReason = message.Headers.FirstOrDefault(x => x.Key.Equals(TmqHeaders.NEGATIVE_ACKNOWLEDGE_REASON, StringComparison.InvariantCultureIgnoreCase));
            descriptor.Set(false, TwinoResult.Failed(nackReason.Value));
        }

        /// <summary>
        /// This method process the response message, when it is received
        /// </summary>
        public void ProcessResponse(TmqMessage message)
        {
            if (message.Type != MessageType.Response || string.IsNullOrEmpty(message.MessageId))
                return;

            MessageDescriptor descriptor;
            lock (_descriptors)
                descriptor = _descriptors.Find(x => x.Message.PendingResponse && x.Message.MessageId == message.MessageId);

            if (descriptor == null)
                return;

            descriptor.Completed = true;
            descriptor.Set(true, message);
        }

        /// <summary>
        /// Starts to follow message acknowledge
        /// </summary>
        public Task<TwinoResult> FollowAcknowledge(TmqMessage message)
        {
            if (!message.PendingAcknowledge || string.IsNullOrEmpty(message.MessageId))
                return Task.FromResult(TwinoResult.Failed());

            DateTime expiration = DateTime.UtcNow + _client.AcknowledgeTimeout;
            AcknowledgeMessageDescriptor descriptor = new AcknowledgeMessageDescriptor(message, expiration);

            lock (_descriptors)
                _descriptors.Add(descriptor);

            return descriptor.Source.Task;
        }

        /// <summary>
        /// Starts to follow message response
        /// </summary>
        public async Task<TmqMessage> FollowResponse(TmqMessage message)
        {
            if (!message.PendingResponse || string.IsNullOrEmpty(message.MessageId))
                return default;

            DateTime expiration = DateTime.UtcNow + _client.ResponseTimeout;
            ResponseMessageDescriptor descriptor = new ResponseMessageDescriptor(message, expiration);

            lock (_descriptors)
                _descriptors.Add(descriptor);

            return await descriptor.Source.Task;
        }

        /// <summary>
        /// Cancels following response of the message
        /// </summary>
        public void UnfollowMessage(TmqMessage message)
        {
            lock (_descriptors)
            {
                int index = _descriptors.FindIndex(x => x.Message.MessageId == message.MessageId);
                if (index < 0)
                    return;
                _descriptors.RemoveAt(index);
            }
        }
    }
}