using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Data for the connection
    /// </summary>
    public class ConnectionData
    {
        /// <summary>
        /// Connection request path or another data like this
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Connection request method or another data like this
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Connection key value properties (if HTTP, header key and values)
        /// </summary>
        public Dictionary<string, string> Properties { get; private set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// dictionary definition with StringComparer.InvariantCultureIgnoreCase !HIGHLY RECOMMENDED!
        /// </summary>
        public void SetProperties(Dictionary<string, string> invariantCultureIgnoreCaseDictionary)
        {
            Properties = invariantCultureIgnoreCaseDictionary;
        }

        /// <summary>
        /// Reads values from stream
        /// </summary>
        public async Task ReadFromStream(Stream stream)
        {
            using StreamReader reader = new StreamReader(stream);
            bool first = true;
            while (!reader.EndOfStream)
            {
                string line = await reader.ReadLineAsync();
                if (first)
                {
                    first = false;
                    int index = line.IndexOf(' ');
                    if (!line.Contains(':') && index > 0)
                    {
                        Method = line.Substring(0, index);
                        Path = line.Substring(index + 1);
                        continue;
                    }
                }

                int i = line.IndexOf(':');
                if (i < 0)
                    continue;

                Properties.Add(line.Substring(0, i), line.Substring(i + 1));
            }
        }
    }
}