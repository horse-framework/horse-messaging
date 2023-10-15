using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues.Internal
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
        //private readonly List<MessageDescriptor> _descriptors = new List<MessageDescriptor>();
        private readonly SortedDictionary<string, MessageDescriptor> _sortedDescriptors = new SortedDictionary<string, MessageDescriptor>(StringComparer.InvariantCultureIgnoreCase);

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
        private readonly HorseClient _client;

        public MessageTracker(HorseClient client)
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
            _temp.Clear();

            lock (_sortedDescriptors)
            {
                if (_sortedDescriptors.Count < 1)
                    return;

                foreach (MessageDescriptor descriptor in _sortedDescriptors.Values)
                {
                    if (descriptor.Completed || descriptor.Expiration < DateTime.UtcNow)
                        _temp.Add(descriptor);
                }

                foreach (MessageDescriptor descriptor in _temp)
                {
                    _sortedDescriptors.Remove(descriptor.Message.MessageId);
                }
            }

            foreach (MessageDescriptor descriptor in _temp)
            {
                if (descriptor.Completed)
                    continue;

                descriptor.Set(false, new HorseResult(HorseResultCode.Failed, "timeout"));
            }
        }

        /// <summary>
        /// Marks all messages as expired
        /// </summary>
        internal void MarkAllMessagesExpired()
        {
            List<MessageDescriptor> temp;
            lock (_sortedDescriptors)
            {
                temp = new List<MessageDescriptor>(_sortedDescriptors.Values);
                _sortedDescriptors.Clear();
            }

            foreach (MessageDescriptor descriptor in temp)
            {
                descriptor.Completed = true;
                descriptor.Set(false, new HorseResult(HorseResultCode.Failed, "timeout"));
            }
        }

        /// <summary>
        /// This method process the response message, when it is received
        /// </summary>
        public void Process(HorseMessage message)
        {
            if (message.Type != MessageType.Response || string.IsNullOrEmpty(message.MessageId))
                return;

            lock (_sortedDescriptors)
            {
                _sortedDescriptors.TryGetValue(message.MessageId, out MessageDescriptor descriptor);
                //MessageDescriptor descriptor = _descriptors.Find(x => x.Message.WaitResponse && x.Message.MessageId == message.MessageId && !x.Completed);

                if (descriptor == null || descriptor.Completed || !descriptor.Message.WaitResponse)
                    return;

                descriptor.Completed = true;
                descriptor.Set(true, message);
            }
        }

        /// <summary>
        /// Starts to follow message response
        /// </summary>
        public async Task<HorseMessage> Track(HorseMessage message)
        {
            if (!message.WaitResponse || string.IsNullOrEmpty(message.MessageId))
                return default;

            DateTime expiration = DateTime.UtcNow + _client.ResponseTimeout;
            ResponseMessageDescriptor descriptor = new ResponseMessageDescriptor(message, expiration);

            lock (_sortedDescriptors)
                _sortedDescriptors.Add(message.MessageId, descriptor);

            return await descriptor.Source.Task;
        }

        public void TrackMultiple(List<HorseMessage> messages, Action<HorseMessage, bool> callback)
        {
            List<CallbackMessageDescriptor> descriptors = new List<CallbackMessageDescriptor>(messages.Count);
            DateTime expiration = DateTime.UtcNow + _client.ResponseTimeout;

            foreach (HorseMessage message in messages)
            {
                if (!message.WaitResponse || string.IsNullOrEmpty(message.MessageId))
                    throw new Exception("Messages must have MessageId and true value for WaitForResponse property");

                CallbackMessageDescriptor descriptor = new CallbackMessageDescriptor(message, expiration, callback);
                descriptors.Add(descriptor);
            }

            lock (_sortedDescriptors)
            {
                foreach (CallbackMessageDescriptor descriptor in descriptors)
                {
                    _sortedDescriptors.Add(descriptor.Message.MessageId, descriptor);
                }
            }
        }

        /// <summary>
        /// Cancels following response of the message
        /// </summary>
        public void Forget(HorseMessage message)
        {
            lock (_sortedDescriptors)
            {
                _sortedDescriptors.Remove(message.MessageId);
            }
        }
    }
}