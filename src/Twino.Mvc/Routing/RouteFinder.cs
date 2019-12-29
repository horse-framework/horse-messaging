using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Twino.Mvc.Results;
using Twino.Protocols.Http;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Twino MVC Default Route finder.
    /// Route finder is used for finding matched first route in route list from Request.Path
    /// </summary>
    public class RouteFinder : IRouteFinder
    {
        /// <summary>
        /// Finds file from request url
        /// </summary>
        public IActionResult FindFile(IEnumerable<FileRoute> routes, HttpRequest request)
        {
            FileRoute route = routes.FirstOrDefault(x => request.Path.StartsWith(x.VirtualPath, StringComparison.InvariantCultureIgnoreCase));
            if (route == null)
                return null;

            bool found = false;
            string fullpath = null;
            foreach (string physicalPath in route.PhysicalPaths)
            {
                fullpath = request.Path.Replace(route.VirtualPath, physicalPath);
                if (File.Exists(fullpath))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return null;

            //check validation
            if (route.Validation != null)
            {
                HttpStatusCode code = route.Validation(request);
                if (code != HttpStatusCode.OK)
                    return new FileResult(code);
            }

            int fileStartIndex = fullpath.LastIndexOf('/');
            string filename = fullpath.Substring(fileStartIndex + 1);

            FileStream stream = new FileStream(fullpath, FileMode.Open, FileAccess.Read);
            return new FileResult(stream, filename);
        }

        private RouteLeaf FindRouteInLeaf(RouteLeaf leaf, string method, string[] parts, int index)
        {
            //for text parts, it should match
            if (leaf.Path.Type == RouteType.Text)
            {
                bool matched = leaf.Path.Value.Equals(parts[index], StringComparison.InvariantCultureIgnoreCase);
                if (!matched)
                    return null;
            }

            //if leaf is last of path check method and return if equals
            if (leaf.Route != null)
            {
                if (parts.Length == index + 1 && leaf.Route.Method.Equals(method, StringComparison.InvariantCultureIgnoreCase))
                    return leaf;

                return null;
            }

            if (leaf.Children.Count == 0)
                return null;

            //parts are done but route may keep going to optional parameters
            if (parts.Length == index + 1)
            {
                RouteLeaf x = leaf;
                while (x.Children.Count == 1)
                {
                    RouteLeaf y = x.Children[0];

                    if (y.Path.Type == RouteType.Text && string.IsNullOrEmpty(y.Path.Value))
                    {
                        if (y.Route.Method.Equals(method, StringComparison.InvariantCultureIgnoreCase))
                            return y;

                        return null;
                    }
                    
                    if (y.Path.Type != RouteType.OptionalParameter)
                        break;

                    if (y.Route != null)
                    {
                        if (y.Route.Method.Equals(method, StringComparison.InvariantCultureIgnoreCase))
                            return y;

                        break;
                    }
                    
                    x = y;
                }
            }

            if (parts.Length < index + 2)
                return null;

            int next = index + 1;
            foreach (RouteLeaf child in leaf.Children)
            {
                RouteLeaf found = FindRouteInLeaf(child, method, parts, next);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Finds matched route from the list with specified request
        /// </summary>
        public RouteMatch Find(IEnumerable<RouteLeaf> routes, HttpRequest request)
        {
            //split path to route parts
            string[] parts = request.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                parts = new[] {""};

            RouteLeaf route = null;
            foreach (RouteLeaf leaf in routes)
            {
                RouteLeaf found = FindRouteInLeaf(leaf, request.Method, parts, 0);
                if (found != null)
                {
                    route = found;
                    break;
                }
            }

            if (route == null)
                return null;

            RouteMatch match = new RouteMatch();
            match.Values = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

            match.Route = route.Route;

            do
            {
                RoutePath path = route.Path;

                //if type is optional parameter we dont need to check if equals
                //we just need to read the part value and put it into value list (if doesn't exists put default value)
                if (path.Type == RouteType.OptionalParameter)
                    match.Values.Add(path.Value,
                                     parts.Length <= route.Index
                                         ? null
                                         : parts[route.Index]);

                //if type is parameter we dont need to check if equals
                //we just need to read the part value and put it into value list
                else if (path.Type == RouteType.Parameter)
                    match.Values.Add(path.Value, parts[route.Index]);

                route = route.Parent;
            } while (route != null);

            return match;
        }
    }
}