using System.Collections.Generic;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Routing
{
    public class RouterDefinition
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public RouteMethod Method { get; set; }
        public List<BindingDefinition> Bindings { get; set; }
    }
}