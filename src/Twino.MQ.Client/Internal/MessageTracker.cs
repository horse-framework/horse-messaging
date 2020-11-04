using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Internal
{
    /// <summary>
    /// Message tracker, tracker the messages which required response.
    /// If their response messages isn't received, fires timeout actions.
    /// </summary>
    internal class MessageTracker : IDisposable
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

        public MessageTracker(TmqClient client)
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
            {
                temp = new List<MessageDescriptor>(_descriptors);
                _descriptors.Clear();
            }

            foreach (MessageDescriptor descriptor in temp)
            {
                descriptor.Completed = true;
                descriptor.Set(false, new TwinoResult(TwinoResultCode.Failed, "timeout"));
            }
        }

        /// <summary>
        /// This method process the response message, when it is received
        /// </summary>
        public void Process(TwinoMessage message)
        {
            if (message.Type != MessageType.Response || string.IsNullOrEmpty(message.MessageId))
                return;

            MessageDescriptor descriptor;
            lock (_descriptors)
                descriptor = _descriptors.Find(x => x.Message.WaitResponse && x.Message.MessageId == message.MessageId);

            if (descriptor == null)
                return;

            descriptor.Completed = true;
            descriptor.Set(true, message);
        }

        /// <summary>
        /// Starts to follow message response
        /// </summary>
        public async Task<TwinoMessage> Track(TwinoMessage message)
        {
            if (!message.WaitResponse || string.IsNullOrEmpty(message.MessageId))
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
        public void Forget(TwinoMessage message)
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