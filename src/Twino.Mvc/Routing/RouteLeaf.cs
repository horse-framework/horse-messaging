using System;
using System.Collections.Generic;

namespace Twino.Mvc.Routing
{
    public class RouteLeaf
    {
        public int Index { get; }
        public RoutePath Path { get; internal set; }

        public Route Route { get; internal set; }

        public List<RouteLeaf> Children { get; internal set; }

        public RouteLeaf Parent { get; }

        public RouteLeaf(RoutePath path, RouteLeaf parent)
        {
            Children = new List<RouteLeaf>();

            Path = path;
            Parent = parent;

            if (parent != null)
                Index = parent.Index + 1;
        }

        public bool PathEquals(RoutePath path)
        {
            return path.Type == Path.Type && path.Value.Equals(Path.Value, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}