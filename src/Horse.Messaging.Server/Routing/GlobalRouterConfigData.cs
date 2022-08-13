using System.Collections.Generic;

namespace Horse.Messaging.Server.Routing;

internal class GlobalRouterConfigData
{
    public List<RouterConfiguration> Routers { get; set; } = new List<RouterConfiguration>();
}