using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
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

                descriptor.Set(null);
            }
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
                descriptor = _descriptors.Find(x => x.Message.AcknowledgeRequired && x.Message.MessageId == message.MessageId);

            if (descriptor == null)
                return;

            if (message.Content != null && message.Length > 0)
            {
                string response = message.ToString();
                if (response.Equals("FAILED", StringComparison.InvariantCultureIgnoreCase) ||
                    response.Equals("TIMEOUT", StringComparison.InvariantCultureIgnoreCase))
                {
                    descriptor.Completed = true;
                    descriptor.Set(null);
                    return;
                }
            }

            descriptor.Completed = true;
            descriptor.Set(new object());
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
                descriptor = _descriptors.Find(x => x.Message.ResponseRequired && x.Message.MessageId == message.MessageId);

            if (descriptor == null)
                return;

            descriptor.Completed = true;
            descriptor.Set(message);
        }

        /// <summary>
        /// Starts to follow message acknowledge
        /// </summary>
        public async Task<bool> FollowAcknowledge(TmqMessage message)
        {
            if (!message.AcknowledgeRequired || string.IsNullOrEmpty(message.MessageId))
                return false;

            DateTime expiration = DateTime.UtcNow + _client.AcknowledgeTimeout;
            AcknowledgeMessageDescriptor descriptor = new AcknowledgeMessageDescriptor(message, expiration);

            lock (_descriptors)
                _descriptors.Add(descriptor);

            return await descriptor.Source.Task;
        }

        /// <summary>
        /// Starts to follow message response
        /// </summary>
        public async Task<TmqMessage> FollowResponse(TmqMessage message)
        {
            if (!message.ResponseRequired || string.IsNullOrEmpty(message.MessageId))
                return default;

            DateTime expiration = DateTime.UtcNow + _client.ResponseTimeout;
            ResponseMessageDescriptor descriptor = new ResponseMessageDescriptor(message, expiration);

            lock (_descriptors)
                _descriptors.Add(descriptor);

            return await descriptor.Source.Task;
        }
    }
}