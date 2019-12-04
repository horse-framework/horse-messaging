using System;

namespace Test.Mq.Internal
{
    internal static class Helpers
    {
        private static readonly Random _random = new Random();

        internal static int GetRandom()
        {
            return _random.Next(100, 5000000);
        }
    }
}