using System.Collections.Generic;

namespace Twino.Core
{
    public class ConnectionData
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}