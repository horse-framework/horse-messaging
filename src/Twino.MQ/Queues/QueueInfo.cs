using System;
using System.Threading;

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
        public long HighPriorityMessagesInQueue => _highPriorityMessagesInQueue;

        private long _highPriorityMessagesInQueue;

        /// <summary>
        /// Pending regular message count in queue
        /// </summary>
        public long RegularMessagesInQueue => _regularMessagesInQueue;

        private long _regularMessagesInQueue;

        
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
        public long ConsumerReceived => _consumerReceived;

        private long _consumerReceived;

        /// <summary>
        /// Timed out message count
        /// </summary>
        public long TimedOutMessages => _timedOutMessages;

        private long _timedOutMessages;

        /// <summary>
        /// Timed out acknowledge count
        /// </summary>
        public long AcknowledgedMessages => _acknowledgedMessages;

        /// <summary>
        /// Timed out acknowledge count
        /// </summary>
        public long _acknowledgedMessages;

        /// <summary>
        /// Acknowledge timed out message count.
        /// When a message is sent to x consumers, x ack should be received.
        /// If received ack messages less than x, this value will increase.
        /// </summary>
        public long TimedOutAcknowledges => _timedOutAcknowledges;

        private long _timedOutAcknowledges;

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
        public DateTime? LastMessageReceivedDate { get; private set; }

        /// <summary>
        /// Last message sent date
        /// </summary>
        public DateTime? LastMessageSendDate { get; private set; }

        /// <summary>
        /// Queue status name
        /// </summary>
        public string Status { get; internal set; }

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
            Volatile.Write(ref _consumerReceived, 0);
            Volatile.Write(ref _timedOutMessages, 0);
            Volatile.Write(ref _acknowledgedMessages, 0);
            Volatile.Write(ref _timedOutAcknowledges, 0);
            Volatile.Write(ref _messageRemoved, 0);
            Volatile.Write(ref _messageSaved, 0);
            Volatile.Write(ref _errorCount, 0);
            Volatile.Write(ref _highPriorityMessagesInQueue, 0);
            Volatile.Write(ref _regularMessagesInQueue, 0);

            LastMessageReceivedDate = null;
            LastMessageSendDate = null;
        }

        #endregion

        #region Increments

        /// <summary>
        /// Increases message receive count
        /// </summary>
        internal void AddMessageReceive()
        {
            LastMessageReceivedDate = DateTime.UtcNow;
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
        internal void AddConsumerReceive()
        {
            Interlocked.Increment(ref _consumerReceived);
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
            Interlocked.Increment(ref _acknowledgedMessages);
        }

        /// <summary>
        /// Increases acknowledge timeout count
        /// </summary>
        internal void AddAcknowledgeTimeout()
        {
            Interlocked.Increment(ref _timedOutAcknowledges);
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
            _highPriorityMessagesInQueue = value;
        }

        /// <summary>
        /// Updates regular message count in queue
        /// </summary>
        internal void UpdateRegularMessageCount(long value)
        {
            _regularMessagesInQueue = value;
        }
        
        #endregion
    }
}