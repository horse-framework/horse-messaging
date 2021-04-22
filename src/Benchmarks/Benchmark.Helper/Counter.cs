using System;
using System.Threading;

namespace Benchmark.Channel.Publisher
{
    public struct Count
    {
        public int ChangeInSecond;
        public int Total;

        public Count(int changeInSecond, int total)
        {
            ChangeInSecond = changeInSecond;
            Total = total;
        }
    }

    public class Counter
    {
        private int _previous;
        private int _count;
        private Timer _timer;

        public void Run(Action<Count> secondAction)
        {
            _timer = new Timer(s =>
            {
                if (_count == 0)
                    return;
                
                int c = _count;
                int diff = c - _previous;
                _previous = c;
                secondAction(new Count(diff, c));
            }, null, 1000, 1000);
        }

        public void Increase()
        {
            Interlocked.Increment(ref _count);
        }
    }
}