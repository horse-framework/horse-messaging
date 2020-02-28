using System;
using System.Collections.Generic;

namespace Twino.SerializableModel.Models
{
    /// <summary>
    /// Description for subscription of reading message from web socket
    /// </summary>
    internal class PackageDescriptor
    {
        /// <summary>
        /// Package type code
        /// </summary>
        public int No { get; set; }

        /// <summary>
        /// Package data type
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// Actions of package received event
        /// </summary>
        public List<Delegate> Actions { get; set; }
    }
}