using System;
using System.Diagnostics;
using System.Threading;

namespace Twino.Core
{
    public delegate void TimerExceptionHandler(ThreadTimer sender, Exception exception);

    public class ThreadTimer
    {
        public event TimerExceptionHandler OnException;

        public bool WaitTickCompletion { get; set; } = true;
        public bool IsRunning { get; private set; }
        public TimeSpan Interval { get; }

        private Thread _thread;
        private readonly Action _action;

        public ThreadTimer(Action action, TimeSpan interval)
        {
            Interval = interval;
            _action = action;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Start()
        {
            if (_thread != null)
                throw new InvalidOperationException("Timer is already running");

            IsRunning = true;

            _thread = new Thread(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (IsRunning)
                {
                    if (WaitTickCompletion)
                        Execute();
                    else
                    {
                        Thread t = new Thread(Execute);
                        t.IsBackground = true;
                        t.Start();
                    }

                    sw.Stop();

                    if (Interval > sw.Elapsed)
                        Thread.Sleep(Interval - sw.Elapsed);

                    sw.Reset();

                    if (IsRunning)
                        sw.Start();
                }

                if (sw.IsRunning)
                    sw.Stop();
            });

            _thread.IsBackground = true;
            _thread.Start();
        }

        public void Stop()
        {
            IsRunning = false;
            _thread.Abort();
            _thread = null;
        }

        private void Execute()
        {
            try
            {
                _action();
            }
            catch (Exception ex)
            {
                OnException?.Invoke(this, ex);
            }
        }
    }
}