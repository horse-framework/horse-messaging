using System.Collections.Generic;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Routing
{
    internal class RouterConfigData
    {
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
        public RouteMethod Method { get; set; }
        public List<BindingConfigData> Bindings { get; set; } = new List<BindingConfigData>();
    }
}