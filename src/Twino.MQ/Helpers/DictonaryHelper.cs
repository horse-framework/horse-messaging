using System.Collections.Generic;

namespace Twino.MQ.Helpers
{
    internal static class DictonaryHelper
    {
        internal static string GetStringValue(this Dictionary<string, string> dictionary, string key)
        {
            string value;
            bool found = dictionary.TryGetValue(key, out value);
            return found ? value.Trim() : null;
        }
    }
}