using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.JsonModel;

namespace Twino.Protocols.WebSocket.Requests
{
    /// <summary>
    /// Response types
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>
        /// Request is sent and response is successfuly received 
        /// </summary>
        Success = 1,
        
        /// <summary>
        /// Request is sent, connection is still alive but time is out
        /// </summary>
        Timeout = 2,
        
        /// <summary>
        /// Request sent, response is received but server returned failed result
        /// </summary>
        Failed = 3,
        
        /// <summary>
        /// Request is sent but before the response is received, the TCP connection is closed
        /// </summary>
        ConnectionError = 4
    }

    /// <summary>
    /// Generic model for response model type of Request Manager
    /// </summary>
    public class SocketResponse<TModel> : SocketResponse where TModel : ISerializableModel, new()
    {
        /// <summary>
        /// Process model
        /// </summary>
        [JsonProperty("model")]
        [JsonPropertyName("model")]
        public TModel Model { get; set; }
    }
    
    /// <summary>
    /// base model for response model type of Request Manager
    /// </summary>
    public class SocketResponse
    {
        /// <summary>
        /// Unique id, same with it's request unique id
        /// </summary>
        [JsonProperty("unique")]
        [JsonPropertyName("unique")]
        public string Unique { get; set; }

        /// <summary>
        /// Requested type code
        /// </summary>
        [JsonProperty("requestType")]
        [JsonPropertyName("requestType")]
        public int RequestType { get; set; }

        /// <summary>
        /// Response type code
        /// </summary>
        [JsonProperty("responseType")]
        [JsonPropertyName("responseType")]
        public int ResponseType { get; set; }

        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public ResponseStatus Status { get; set; }
    }
}