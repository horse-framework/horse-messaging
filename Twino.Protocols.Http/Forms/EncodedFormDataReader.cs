using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Twino.Protocols.Http.Forms
{
    /// <summary>
    /// Reads and parses url-encoded
    /// </summary>
    public static class EncodedFormDataReader
    {
        /// <summary>
        /// Reads and parse url-encoded form data from stream content
        /// </summary>
        public static Dictionary<string, string> Read(MemoryStream stream)
        {
            if (stream == null)
                return new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            string content = Encoding.UTF8.GetString(stream.ToArray());
            return Read(content);
        }

        /// <summary>
        /// Reads and parse url-encoded form data from string content
        /// </summary>
        public static Dictionary<string, string> Read(string content)
        {
            Dictionary<string, string> items = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (string.IsNullOrEmpty(content))
                return items;

            string[] pairs = content.Split('&');
            foreach (string pair in pairs)
            {
                string[] key_value = pair.Split('=');
                if (key_value.Length != 2)
                    continue;

                string key = key_value[0];
                string value = System.Net.WebUtility.HtmlDecode(key_value[1]);

                if (items.ContainsKey(key))
                    items[key] = "," + value;
                else
                    items.Add(key, value);
            }

            return items;
        }
    }
}