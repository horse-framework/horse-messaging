using Newtonsoft.Json;

namespace Twino.JsonModel
{
    /// <summary>
    /// Type and data specified socket model, created for compact usage
    /// </summary>
    public class SerializableModel<T> : ISerializableModel where T : class, new()
    {
        /// <summary>
        /// Type code for model
        /// </summary>
        [JsonProperty("type")]
        public int Type { get; set; }

        /// <summary>
        /// Model generic data
        /// </summary>
        [JsonProperty("data")]
        public T Data { get; set; }
    }
}