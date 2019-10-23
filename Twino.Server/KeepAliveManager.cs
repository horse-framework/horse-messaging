using System;

namespace Twino.Server
{
    internal class KeepAliveManager
    {
        private readonly TimeoutHandler[] _timeoutHandlers = new TimeoutHandler[10];

        public bool IsRunning { get; private set; }
        private int _nextIndex;

        public void Start(int timeoutMilliseconds)
        {
            if (IsRunning)
                throw new InvalidOperationException("Keep Alive Manager is already running");

            IsRunning = true;
            _nextIndex = 0;
            Random rnd = new Random();
            
            for (int i = 0; i < _timeoutHandlers.Length; i++)
            {
                TimeoutHandler handler = new TimeoutHandler(timeoutMilliseconds, rnd.Next(500, 1500));
                _timeoutHandlers[i] = handler;
                handler.Start();
            }
        }

        public void Stop()
        {
            IsRunning = false;
            
            foreach (var handler in _timeoutHandlers)
            {
                if (handler == null)
                    continue;

                handler.Stop();
            }
        }

        /// <summary>
        /// Adds new connection to keep alive manager.
        /// This connection's timeout will be set in this method and starts it's timeout span
        /// </summary>
        public void Add(HandshakeInfo info)
        {
            int i = _nextIndex++;
            if (i >= _timeoutHandlers.Length)
                i = 0;

            _timeoutHandlers[i].Add(info);
        }
    }
}