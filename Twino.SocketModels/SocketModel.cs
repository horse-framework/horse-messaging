using Newtonsoft.Json;

namespace Twino.SocketModels
{
    /// <summary>
    /// Type and data specified socket model, created for compact usage
    /// </summary>
    public class SocketModel<T> : ISocketModel where T : class, new()
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