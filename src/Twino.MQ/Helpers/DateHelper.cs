using System;

namespace Twino.MQ.Helpers
{
    internal static class DateHelper
    {
        public static long ToUnixMilliseconds(this DateTime date)
        {
            TimeSpan span = date - new DateTime(1970, 1, 1);
            return Convert.ToInt64(span.TotalMilliseconds);
        }

        public static long LifetimeMilliseconds(this DateTime date)
        {
            return Convert.ToInt64((DateTime.UtcNow - date).TotalMilliseconds);
        }
    }
}