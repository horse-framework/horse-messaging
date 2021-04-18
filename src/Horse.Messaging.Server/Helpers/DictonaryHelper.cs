using System.Collections.Generic;

namespace Horse.Messaging.Server.Helpers
{
    internal static class DictonaryHelper
    {
        /// <summary>
        /// Gets string value and trims if exists.
        /// If key not found, returns default value
        /// </summary>
        internal static string GetStringValue(this Dictionary<string, string> dictionary, string key)
        {
            string value;
            bool found = dictionary.TryGetValue(key, out value);
            return found ? value.Trim() : null;
        }
    }
}