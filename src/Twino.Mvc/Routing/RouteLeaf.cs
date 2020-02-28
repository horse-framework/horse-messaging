using System;
using System.Collections.Generic;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Leaf for a route definition.
    /// Each route points a word between two slashes.
    /// </summary>
    public class RouteLeaf
    {
        /// <summary>
        /// Leaf index. Root leaves are with 0 index.
        /// eg: site.com/leaf-index-0/leaf-index-1/...
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Path type and value for leaf
        /// </summary>
        public RoutePath Path { get; internal set; }

        /// <summary>
        /// If leaf points a valid route, the route information.
        /// For example the real route is /account/login,
        /// "account" leaf does not point a valid route, it's route is null
        /// but "login" leaf points the login action in account controller it has a valid route.
        /// </summary>
        public Route Route { get; internal set; }

        /// <summary>
        /// Child leaves of the leaf
        /// </summary>
        public List<RouteLeaf> Children { get; internal set; }

        /// <summary>
        /// Parent leaf of the leaf
        /// </summary>
        public RouteLeaf Parent { get; }

        /// <summary>
        /// Creates new leaf in specified path
        /// </summary>
        public RouteLeaf(RoutePath path, RouteLeaf parent)
        {
            Children = new List<RouteLeaf>();

            Path = path;
            Parent = parent;

            if (parent != null)
                Index = parent.Index + 1;
        }

        /// <summary>
        /// Return true, if leaf equals to specified path
        /// </summary>
        public bool PathEquals(RoutePath path)
        {
            return path.Type == Path.Type && path.Value.Equals(Path.Value, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}