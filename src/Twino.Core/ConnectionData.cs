using System;
using System.Collections.Generic;

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
    }
}