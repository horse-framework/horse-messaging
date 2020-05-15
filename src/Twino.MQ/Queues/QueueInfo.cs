using System;
using System.Threading;
using Twino.MQ.Helpers;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Queue statistics information
    /// </summary>
    public class QueueInfo
    {
        #region Properties

        /// <summary>
        /// Queue creation date
        /// </summary>
        public DateTime CreatedDate { get; }

        /// <summary>
        /// Pending high priority message count in queue
        /// </summary>
        public long InQueueHighPriorityMessages => _inQueueHighPriorityMessages;

        private long _inQueueHighPriorityMessages;

        /// <summary>
        /// Pending regular message count in queue
        /// </summary>
        public long InQueueRegularMessages => _inQueueRegularMessages;

        private long _inQueueRegularMessages;


        /// <summary>
        /// Total received messages from producers
        /// </summary>
        public long ReceivedMessages => _receivedMessages;

        private long _receivedMessages;

        /// <summary>
        /// Total send messages.
        /// Each message may be sent to many consumers but all of them counts one.
        /// </summary>
        public long SentMessages => _sentMessages;

        private long _sentMessages;

        /// <summary>
        /// Total consumer message receive count.
        /// If 2 are sent to 5 consumers, this value will be 10.
        /// </summary>
        public long Deliveries => _deliveries;

        private long _deliveries;

        /// <summary>
        /// Timed out message count
        /// </summary>
        public long TimedOutMessages => _timedOutMessages;

        private long _timedOutMessages;

        /// <summary>
        /// Timed out acknowledge count
        /// </summary>
        public long Acknowledges => _acknowledges;

        /// <summary>
        /// Timed out acknowledge count
        /// </summary>
        private long _acknowledges;

        /// <summary>
        /// Acknowledge timed out message count.
        /// When a message is sent to x consumers, x ack should be received.
        /// If received ack messages less than x, this value will increase.
        /// </summary>
        public long NegativeAcknowledge => _negativeAcknowledge;

        private long _negativeAcknowledge;

        /// <summary>
        /// Removed message count
        /// </summary>
        public long MessageRemoved => _messageRemoved;

        private long _messageRemoved;

        /// <summary>
        /// Saved message count
        /// </summary>
        public long MessageSaved => _messageSaved;

        private long _messageSaved;

        /// <summary>
        /// Error count
        /// </summary>
        public long ErrorCount => _errorCount;

        private long _errorCount;

        /// <summary>
        /// Last message received date
        /// </summary>
        public DateTime? LastMessageReceiveDate { get; private set; }

        /// <summary>
        /// Last message sent date
        /// </summary>
        public DateTime? LastMessageSendDate { get; private set; }

        /// <summary>
        /// Queue status name
        /// </summary>
        public string Status { get; internal set; }

        /// <summary>
        /// Returns last message received date in unix milliseconds
        /// </summary>
        public long GetLastMessageReceiveUnix()
        {
            if (!LastMessageReceiveDate.HasValue)
                return 0;

            return LastMessageReceiveDate.Value.ToUnixMilliseconds();
        }

        /// <summary>
        /// Returns last message sent date in unix milliseconds
        /// </summary>
        public long GetLastMessageSendUnix()
        {
            if (!LastMessageSendDate.HasValue)
                return 0;

            return LastMessageSendDate.Value.ToUnixMilliseconds();
        }

        #endregion

        #region Constructor - Reset

        /// <summary>
        /// Creates new queue statistics information object
        /// </summary>
        public QueueInfo()
        {
            CreatedDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Resets all statistics for the queue
        /// </summary>
        public void Reset()
        {
            Volatile.Write(ref _receivedMessages, 0);
            Volatile.Write(ref _sentMessages, 0);
            Volatile.Write(ref _deliveries, 0);
            Volatile.Write(ref _timedOutMessages, 0);
            Volatile.Write(ref _acknowledges, 0);
            Volatile.Write(ref _negativeAcknowledge, 0);
            Volatile.Write(ref _messageRemoved, 0);
            Volatile.Write(ref _messageSaved, 0);
            Volatile.Write(ref _errorCount, 0);
            Volatile.Write(ref _inQueueHighPriorityMessages, 0);
            Volatile.Write(ref _inQueueRegularMessages, 0);

            LastMessageReceiveDate = null;
            LastMessageSendDate = null;
        }

        #endregion

        #region Increments

        /// <summary>
        /// Increases message receive count
        /// </summary>
        internal void AddMessageReceive()
        {
            LastMessageReceiveDate = DateTime.UtcNow;
            Interlocked.Increment(ref _receivedMessages);
        }

        /// <summary>
        /// Increases message send count
        /// </summary>
        internal void AddMessageSend()
        {
            LastMessageSendDate = DateTime.UtcNow;
            Interlocked.Increment(ref _sentMessages);
        }

        /// <summary>
        /// Increases consumer messave receive count
        /// </summary>
        internal void AddDelivery()
        {
            Interlocked.Increment(ref _deliveries);
        }

        /// <summary>
        /// Increases message timeout count
        /// </summary>
        internal void AddMessageTimeout()
        {
            Interlocked.Increment(ref _timedOutMessages);
        }

        /// <summary>
        /// Increases acknowledge count
        /// </summary>
        internal void AddAcknowledge()
        {
            Interlocked.Increment(ref _acknowledges);
        }

        /// <summary>
        /// Increases acknowledge timeout count
        /// </summary>
        internal void AddNegativeAcknowledge()
        {
            Interlocked.Increment(ref _negativeAcknowledge);
        }

        /// <summary>
        /// Increases message remove count
        /// </summary>
        internal void AddMessageRemove()
        {
            Interlocked.Increment(ref _messageRemoved);
        }

        /// <summary>
        /// Increases message save count
        /// </summary>
        internal void AddMessageSave()
        {
            Interlocked.Increment(ref _messageSaved);
        }

        /// <summary>
        /// Increases error count
        /// </summary>
        internal void AddError()
        {
            Interlocked.Increment(ref _errorCount);
        }

        /// <summary>
        /// Updates high priority message count in queue
        /// </summary>
        internal void UpdateHighPriorityMessageCount(long value)
        {
            _inQueueHighPriorityMessages = value;
        }

        /// <summary>
        /// Updates regular message count in queue
        /// </summary>
        internal void UpdateRegularMessageCount(long value)
        {
            _inQueueRegularMessages = value;
        }

        #endregion
    }
}