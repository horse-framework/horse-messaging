using System.Collections.Generic;
using System.Text.Json.Serialization;
using Horse.Messaging.Protocol;
using Newtonsoft.Json.Converters;

namespace Horse.Messaging.Server.Routing
{
    public class RouterDefinition
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public RouteMethod Method { get; set; }

        public List<BindingDefinition> Bindings { get; set; }
    }
}