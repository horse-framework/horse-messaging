using System;
using System.Collections.Generic;
using System.Threading;
using Twino.Core;

namespace Twino.SocketModels.Requests
{
    /// <summary>
    /// Handles socket's active pending requests
    /// </summary>
    public class RequestClientHandler
    {
        /// <summary>
        /// When client sends the request, a pending request is created for reading it's response or manage it's timeout.
        /// This dictionary keeps all pending requests until they finished. 
        /// </summary>
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new Dictionary<string, PendingRequest>();

        /// <summary>
        /// Temp error occured pending request list
        /// </summary>
        private readonly List<KeyValuePair<string, PendingRequest>> _errors = new List<KeyValuePair<string, PendingRequest>>();

        /// <summary>
        /// Temp timed out pending request list
        /// </summary>
        private readonly List<KeyValuePair<string, PendingRequest>> _timeouts = new List<KeyValuePair<string, PendingRequest>>();

        /// <summary>
        /// Socket client of all requests in this instance
        /// </summary>
        public SocketBase Socket { get; }

        /// <summary>
        /// Request cleanup timer
        /// </summary>
        private Timer _cleanupTimer;

        public RequestClientHandler(SocketBase socket)
        {
            Socket = socket;
        }

        /// <summary>
        /// Initializes handler, subscribes socket message received event and starts cleanup timer
        /// </summary>
        public void Initialize()
        {
            _cleanupTimer = new Timer(Tick, null, 1000, 1000);
            Socket.MessageReceived += SenderOnMessageReceived;
        }

        /// <summary>
        /// Releases all sources of handler
        /// </summary>
        public void Dispose()
        {
            Socket.MessageReceived -= SenderOnMessageReceived;

            lock (_pendingRequests)
            {
                foreach (var kv in _pendingRequests)
                    kv.Value.CompleteAsError();

                _pendingRequests.Clear();
            }

            if (_cleanupTimer != null)
            {
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }

        /// <summary>
        /// Starts to track the request
        /// </summary>
        internal void Handle(PendingRequest request)
        {
            lock (_pendingRequests)
                _pendingRequests.Add(request.Header.Unique, request);
        }

        /// <summary>
        /// Checks all pending requests, if they are failed or timed out removed them from active pending request list
        /// </summary>
        private void Tick(object state)
        {
            if (_errors.Count > 0)
                _errors.Clear();

            if (_timeouts.Count > 0)
                _timeouts.Clear();

            lock (_pendingRequests)
            {
                foreach (KeyValuePair<string, PendingRequest> pair in _pendingRequests)
                {
                    if (pair.Value.Sender == null || !pair.Value.Sender.IsConnected)
                    {
                        _errors.Add(pair);
                        continue;
                    }

                    if (pair.Value.Deadline > DateTime.UtcNow)
                        continue;

                    _timeouts.Add(pair);
                }

                foreach (var kv in _errors)
                    _pendingRequests.Remove(kv.Key);

                foreach (var kv in _timeouts)
                    _pendingRequests.Remove(kv.Key);
            }

            foreach (KeyValuePair<string, PendingRequest> error in _errors)
                error.Value.CompleteAsError();

            foreach (KeyValuePair<string, PendingRequest> timeout in _timeouts)
                timeout.Value.CompleteAsTimeout();
        }

        /// <summary>
        /// Called when a message received from the socket.
        /// If message is a response, it's proceed.
        /// </summary>
        private void SenderOnMessageReceived(SocketBase client, string message)
        {
            SocketResponse header = TwinoRequestSerializer.DeserializeHeader<SocketResponse>(TwinoRequestSerializer.RESPONSE_CODE, message);

            if (header == null || string.IsNullOrEmpty(header.Unique))
                return;

            PendingRequest pending;
            lock (_pendingRequests)
            {
                bool found = _pendingRequests.TryGetValue(header.Unique, out pending);
                if (!found)
                    return;

                _pendingRequests.Remove(header.Unique);
            }

            switch (header.Status)
            {
                case ResponseStatus.Success:
                    object model = TwinoRequestSerializer.DeserializeModel(pending.ModelType, TwinoRequestSerializer.RESPONSE_CODE, message);
                    pending.CompleteAsSuccessful(model);
                    break;

                case ResponseStatus.Failed:
                    pending.CompleteAsFailed();
                    break;

                case ResponseStatus.Timeout:
                    pending.CompleteAsTimeout();
                    break;

                case ResponseStatus.ConnectionError:
                    pending.CompleteAsError();
                    break;
            }
        }
    }
}