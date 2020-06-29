using System.Text.Json;
using Newtonsoft.Json;

namespace Twino.Mvc
{
    /// <summary>
    /// Serialization options
    /// </summary>
    public class JsonSerializationOptions
    {
        /// <summary>
        /// If true, newtonsoft library is used
        /// </summary>
        public bool UseNewtonsoft { get; set; }

        /// <summary>
        /// System Text Options
        /// </summary>
        public JsonSerializerOptions SystemTextOptions { get; set; }

        /// <summary>
        /// Newtonsoft Options
        /// </summary>
        public JsonSerializerSettings NewtonsoftOptions { get; set; }
    }
}