using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Thread timer exception handler
    /// </summary>
    public delegate void TimerExceptionHandler(ThreadTimer sender, Exception exception);

    /// <summary>
    /// Thread based timer
    /// </summary>
    public class ThreadTimer
    {
        /// <summary>
        /// Triggered when an exception is thrown inside of timer elapse
        /// </summary>
        public event TimerExceptionHandler OnException;

        /// <summary>
        /// If true, next tick waits previous if it's execution time is longer than interval duration.
        /// Default is true
        /// </summary>
        public bool WaitTickCompletion { get; set; } = true;

        /// <summary>
        /// True, if timer is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Timer elapse interval
        /// </summary>
        public TimeSpan Interval { get; }

        private Thread _thread;
        private readonly Action _action;
        private readonly object _locker = new object();

        /// <summary>
        /// Created new thread-based timer
        /// </summary>
        public ThreadTimer(Action action, TimeSpan interval)
        {
            Interval = interval;
            _action = action;
        }

        /// <summary>
        /// Starts the timer
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if timer is already started</exception>
        public void Start(ThreadPriority priority = ThreadPriority.Normal)
        {
            if (_thread != null)
                throw new InvalidOperationException("Timer is already running");

            lock (_locker)
            {
                if (IsRunning)
                    throw new InvalidOperationException("Timer is already running");

                IsRunning = true;
            }

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
            _thread.Priority = priority;
            _thread.Start();
        }

        /// <summary>
        /// Stop the timer and disposes all sources
        /// </summary>
        public void Stop()
        {
            lock (_locker)
                IsRunning = false;

            //throws exception in non-supported platforms such as ubuntu
            try
            {
                _thread.Abort();
            }
            catch
            {
            }

            _thread = null;
        }

        /// <summary>
        /// Executes action in safe and triggers exception event, if an exception is thrown
        /// </summary>
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