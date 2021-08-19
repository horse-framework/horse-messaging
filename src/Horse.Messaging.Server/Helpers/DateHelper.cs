using System;

namespace Horse.Messaging.Server.Helpers
{
    internal static class DateHelper
    {
        /// <summary>
        /// Converts DateTime to unix milliseconds
        /// </summary>
        public static long ToUnixMilliseconds(this DateTime date)
        {
            TimeSpan span = date - new DateTime(1970, 1, 1);
            return Convert.ToInt64(span.TotalMilliseconds);
        }

        /// <summary>
        /// Returns total milliseconds between UTC now and the date
        /// </summary>
        public static long LifetimeMilliseconds(this DateTime date)
        {
            return Convert.ToInt64((DateTime.UtcNow - date).TotalMilliseconds);
        }
    }
}