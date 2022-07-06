using System.Collections.Generic;

namespace Horse.Messaging.Server.Routing;

internal class GlobalRouterConfigData
{
    public List<RouterConfigData> Routers { get; set; } = new List<RouterConfigData>();
}