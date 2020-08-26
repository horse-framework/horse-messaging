using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Pull from queue container
    /// </summary>
    public class PullContainer
    {
        /// <summary>
        /// Pull Request Message Id 
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// Total received message count
        /// </summary>
        public int ReceivedCount { get; private set; }

        /// <summary>
        /// Requested message count
        /// </summary>
        public int RequestCount { get; }

        /// <summary>
        /// Pull container status
        /// </summary>
        public PullProcess Status { get; private set; }

        /// <summary>
        /// UTC Time last message received.
        /// If there is no message received, the time request sent.
        /// </summary>
        public DateTime LastReceived { get; private set; }

        /// <summary>
        /// Received messages
        /// </summary>
        public IEnumerable<TwinoMessage> ReceivedMessages => _messages;

        private readonly List<TwinoMessage> _messages;
        private readonly TaskCompletionSource<PullContainer> _source;
        private readonly Func<int, TwinoMessage, Task> _cycleAction;

        internal PullContainer(string requestId, int requestCount, Func<int, TwinoMessage, Task> cycleAction)
        {
            _source = new TaskCompletionSource<PullContainer>();
            _messages = new List<TwinoMessage>();

            RequestId = requestId;
            RequestCount = requestCount;
            _cycleAction = cycleAction;
            Status = PullProcess.Receiving;
            LastReceived = DateTime.UtcNow;
        }

        internal void AddMessage(TwinoMessage message)
        {
            LastReceived = DateTime.UtcNow;

            lock (_messages)
            {
                _messages.Add(message);
                ReceivedCount = _messages.Count;
            }

            if (_cycleAction != null)
                _ = _cycleAction(ReceivedCount, message);
        }

        internal void Complete(string noContentReason)
        {
            if (string.IsNullOrEmpty(noContentReason))
                Status = PullProcess.Timeout;
            else if (noContentReason.Equals(TmqHeaders.END, StringComparison.InvariantCultureIgnoreCase))
                Status = PullProcess.Completed;
            else if (noContentReason.Equals(TmqHeaders.EMPTY, StringComparison.InvariantCultureIgnoreCase))
                Status = PullProcess.Empty;
            else if (noContentReason.Equals("Error", StringComparison.InvariantCultureIgnoreCase))
                Status = PullProcess.NetworkError;
            else if (noContentReason.Equals(TmqHeaders.UNACCEPTABLE, StringComparison.InvariantCultureIgnoreCase))
                Status = PullProcess.Unacceptable;
            else if (noContentReason.Equals(TmqHeaders.UNAUTHORIZED, StringComparison.InvariantCultureIgnoreCase))
                Status = PullProcess.Unauthorized;
            else
                Status = PullProcess.Completed;

            _source.SetResult(this);
        }

        internal Task<PullContainer> GetAwaitableTask()
        {
            return _source.Task;
        }
    }
}