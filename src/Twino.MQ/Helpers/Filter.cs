using System;

namespace Twino.MQ.Helpers
{
    internal class Filter
    {
        public static bool CheckMatch(string value, string filter)
        {
            bool jstart = filter.StartsWith('*');
            bool jend = filter.EndsWith('*');

            if (jstart && jend)
                return value.Contains(filter.Substring(1, filter.Length - 2), StringComparison.InvariantCultureIgnoreCase);

            if (jstart)
                return value.EndsWith(filter.Substring(1));

            if (jend)
                return value.StartsWith(filter.Substring(0, filter.Length - 1));

            return value.Equals(filter, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}