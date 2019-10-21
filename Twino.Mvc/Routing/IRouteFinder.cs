using System.Collections.Generic;
using Twino.Core.Http;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Route finder implementation.
    /// Route finder is used for finding matched first route in route list from Request.Path
    /// </summary>
    public interface IRouteFinder
    {

        /// <summary>
        /// Finds matched route from the list with specified request
        /// </summary>
        RouteMatch Find(IEnumerable<Route> routes, HttpRequest request);

    }
}
