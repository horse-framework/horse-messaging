using System.Collections.Generic;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Matched route info
    /// </summary>
    public class RouteMatch
    {
        /// <summary>
        /// Route information
        /// </summary>
        public Route Route { get; set; }

        /// <summary>
        /// Route values for the specified match.
        /// </summary>
        public Dictionary<string, object> Values { get; set; }

    }
}
