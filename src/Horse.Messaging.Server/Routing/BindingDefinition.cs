using Horse.Messaging.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Horse.Messaging.Server.Routing
{
    public class BindingDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Target { get; set; }
        public ushort? ContentType { get; set; }
        public int Priority { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public RouteMethod? Method { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public BindingInteraction Interaction { get; set; }
    }
}