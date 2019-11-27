using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.SocketModels.Requests
{
    /// <summary>
    /// The header information for Request Manager requests
    /// </summary>
    public class RequestHeader
    {
        /// <summary>
        /// Request's unique id, will be used for response
        /// </summary>
        [JsonProperty("unique")]
        [JsonPropertyName("unique")]
        public string Unique { get; set; }
        
        /// <summary>
        /// Request type code
        /// </summary>
        [JsonProperty("requestType")]
        [JsonPropertyName("requestType")]
        public int RequestType { get; set; }
        
        /// <summary>
        /// Expected response type code
        /// </summary>
        [JsonProperty("responseType")]
        [JsonPropertyName("responseType")]
        public int ResponseType { get; set; }
    }
}