using System.Collections.Generic;
using System.Threading;
using Twino.Core;

namespace Twino.Protocols.WebSocket.Requests
{
    internal sealed class RequestClientPool
    {
        /// <summary>
        /// When a request is created via a socket client, this client is added to alive connection list until it's disconnected or manually removed
        /// </summary>
        private readonly Dictionary<ClientSocketBase<WebSocketMessage>, RequestClientHandler> _clients =
            new Dictionary<ClientSocketBase<WebSocketMessage>, RequestClientHandler>();

        /// <summary>
        /// Removing client lists
        /// </summary>
        private readonly List<RequestClientHandler> _removing = new List<RequestClientHandler>();

        private static RequestClientPool _instance;

        /// <summary>
        /// Singleton instance of request client pool object
        /// </summary>
        public static RequestClientPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RequestClientPool();
                    _instance.Initialize();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Timer for cleaning up disconnected clients
        /// </summary>
        private Timer _cleanupTimer;

        private RequestClientPool()
        {
        }

        private void Initialize()
        {
            _cleanupTimer = new Timer(Tick, null, 1000, 1000);
        }

        /// <summary>
        /// Ticks, checks active connections and remove if they are disconnected
        /// </summary>
        private void Tick(object state)
        {
            if (_removing.Count > 0)
                _removing.Clear();

            lock (_clients)
            {
                foreach (RequestClientHandler handler in _clients.Values)
                {
                    if (!handler.Socket.IsConnected)
                        _removing.Add(handler);
                }

                foreach (RequestClientHandler handler in _removing)
                    _clients.Remove(handler.Socket);
            }

            if (_removing.Count < 1)
                return;

            foreach (RequestClientHandler handler in _removing)
                handler.Dispose();

            _removing.Clear();
        }

        /// <summary>
        /// Finds handler of the specified socket
        /// </summary>
        internal RequestClientHandler GetHandler(ClientSocketBase<WebSocketMessage> socket)
        {
            RequestClientHandler handler;

            lock (_clients)
            {
                bool found = _clients.TryGetValue(socket, out handler);
                if (found)
                    return handler;

                handler = new RequestClientHandler(socket);
                _clients.Add(socket, handler);
            }

            handler.Initialize();

            return handler;
        }
    }
}