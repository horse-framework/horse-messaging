using System;
using System.Collections.Generic;
using Twino.Core.Http;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Twino MVC Default Route finder.
    /// Route finder is used for finding matched first route in route list from Request.Path
    /// </summary>
    public class RouteFinder : IRouteFinder
    {

        /// <summary>
        /// Finds matched route from the list with specified request
        /// </summary>
        public RouteMatch Find(IEnumerable<Route> routes, HttpRequest request)
        {
            RouteMatch match = new RouteMatch();
            match.Values = new Dictionary<string, object>();

            //split path to route parts
            string[] parts = request.GetOnlyPath().Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                parts = new string[] { "" };

            foreach (Route route in routes)
            {
                //if route's path list is shorter then request match impossible
                //NOTE: this check should not be "==" cuz of optional parameter.
                if (route.Path.Length < parts.Length)
                    continue;

                //check HTTP method
                if (!string.Equals(route.Method, request.Method, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                bool skip = false;

                //part length and HTTP method are checked.
                //we need to check each part of the path
                for (int i = 0; i < route.Path.Length; i++)
                {
                    RoutePath route_part = route.Path[i];

                    //if type is optional parameter we dont need to check if equals
                    //we just need to read the part value and put it into value list (if doesn't exists put default value)
                    if (route_part.Type == RouteType.OptionalParameter)
                    {
                        if (parts.Length <= i)
                            match.Values.Add(route_part.Value, null);
                        else
                            match.Values.Add(route_part.Value, parts[i]);
                    }

                    //if type is parameter we dont need to check if equals
                    //we just need to read the part value and put it into value list
                    else if (route_part.Type == RouteType.Parameter)
                        match.Values.Add(route_part.Value, parts[i]);

                    //if type is not parameter, it should be text, controller or action. for all, the route value contains exact value
                    //NOTE: for [controller] or [action] values, for each and action created different route object and added to route list on MVC Init.
                    //      so in here, we don2t need to try to check patterns etc. just check if the strings are equal.
                    else if (!string.Equals(route_part.Value, parts[i], StringComparison.InvariantCultureIgnoreCase))
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                    continue;

                match.Route = route;
            }

            if (match.Route == null)
                return null;

            return match;
        }

    }
}
