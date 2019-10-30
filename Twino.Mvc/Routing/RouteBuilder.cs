using Twino.Mvc.Controllers.Parameters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters.Route;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// By specified controller type, finds all actions,
    /// Reads all controller attributes, reads all action attributes,
    /// Reads all parameters with their attributes
    /// And creates Route objects for all of them.
    /// These Route object creation operation saves us from a lot of work on each request.
    /// </summary>
    public class RouteBuilder
    {
        private class RouteInfo
        {
            public string Method { get; set; }
            public string Pattern { get; set; }
        }

        /// <summary>
        /// Creates all Route objects of the specified Controller type
        /// </summary>
        public IEnumerable<Route> BuildRoutes(Type controllerType)
        {
            //find base path for the controller
            List<string> controllerRoutes = FindControllerRoutes(controllerType);

            MethodInfo[] methods = controllerType.GetMethods();

            foreach (MethodInfo method in methods)
            {
                //find method route if exists
                List<RouteInfo> methodRoutes = FindActionRoute(method);

                if (methodRoutes.Count == 0)
                    continue;

                foreach (string controllerRoute in controllerRoutes)
                {
                    foreach (RouteInfo info in methodRoutes)
                    {
                        //create fullpath
                        string fullpath = string.IsNullOrEmpty(info.Pattern)
                                              ? controllerRoute
                                              : (controllerRoute + "/" + info.Pattern);

                        //get route table from the fullpath
                        List<RoutePath> path = GetRoutePath(controllerType, method, fullpath);

                        Route route = new Route
                                      {
                                          ActionType = method,
                                          ControllerType = controllerType,
                                          Method = info.Method,
                                          Path = path.ToArray(),
                                          Parameters = BuildParameters(method)
                                      };

                        yield return route;
                    }
                }
            }
        }

        /// <summary>
        /// Finds route value for the controller type
        /// </summary>
        private static List<string> FindControllerRoutes(Type controllerType)
        {
            List<string> routes = new List<string>();

            object[] attributes = controllerType.GetCustomAttributes(true);

            foreach (object attr in attributes)
            {
                RouteAttribute routeAttribute = attr as RouteAttribute;
                if (routeAttribute == null)
                    continue;

                routes.Add(routeAttribute.Pattern);
            }

            //if there is no route definition, the route value is the name of the controller
            if (routes.Count == 0)
            {
                string value = controllerType.Name.ToLower(new CultureInfo("en-US"));

                //remove value from the end
                if (value.EndsWith("controller"))
                    value = value.Substring(0, value.Length - "controller".Length);

                routes.Add(value);
            }

            return routes;
        }

        /// <summary>
        /// Finds route string for the Action.
        /// This methods returns Tuple with 2 strings.
        /// First string represents the HTTP Method,
        /// Second string represents the Route URL
        /// </summary>
        private List<RouteInfo> FindActionRoute(MethodInfo method)
        {
            List<RouteInfo> routes = new List<RouteInfo>();

            object[] attributes = method.GetCustomAttributes(typeof(HttpMethodAttribute), true);

            foreach (object attribute in attributes)
            {
                if (!(attribute is HttpMethodAttribute attr))
                    continue;

                RouteInfo route = new RouteInfo
                                  {
                                      Method = attr.Method,
                                      Pattern = attr.Pattern
                                  };

                routes.Add(route);
            }

            return routes;
        }

        /// <summary>
        /// Creates RoutePath table from the controller type, action method reflection info and full path.
        /// </summary>
        private List<RoutePath> GetRoutePath(Type controller, MethodInfo action, string fullpath)
        {
            List<RoutePath> result = new List<RoutePath>();

            //split path to parts.
            //each part will be replaced to RoutePath class with extra information
            string[] parts = fullpath.ToLower(new CultureInfo("en-US")).Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                RoutePath route = new RoutePath();

                //if the part is controller name, set the value to the specified controller (comes from method parameter)
                if (part == "[controller]")
                {
                    route.Type = RouteType.Controller;

                    string controllerName = controller.Name.ToLower(new CultureInfo("en-US"));
                    if (controllerName.EndsWith("controller"))
                    {
                        int cindex = controllerName.IndexOf("controller", StringComparison.Ordinal);
                        route.Value = controllerName.Substring(0, cindex);
                    }
                    else
                        route.Value = controllerName;
                }

                //if the part is action name, set the value to the specified action method name (comes from method parameter)
                else if (part == "[action]")
                {
                    route.Type = RouteType.Action;
                    route.Value = action.Name.ToLower(new CultureInfo("en-US"));
                }

                //if the part is parameter check if it's optional or not and set value as parameter name
                else if (part.StartsWith('{'))
                {
                    route.Type = part.Substring(1, 1) == "?"
                                     ? RouteType.OptionalParameter
                                     : RouteType.Parameter;

                    route.Value = route.Type == RouteType.OptionalParameter
                                      ? part.Substring(2, part.Length - 3)
                                      : part.Substring(1, part.Length - 2);
                }

                //for all other kinds set the value directly as text type.
                else
                {
                    route.Type = RouteType.Text;
                    route.Value = part;
                }

                result.Add(route);
            }

            if (result.Count == 0)
                result.Add(new RoutePath
                           {
                               Type = RouteType.Text,
                               Value = ""
                           });

            return result;
        }

        /// <summary>
        /// Creates parameter information array objects for the specified action method
        /// </summary>
        private ActionParameter[] BuildParameters(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();

            //result length is same with method's real parameter array length
            ActionParameter[] result = new ActionParameter[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                ParameterSourceAttribute attr = parameter.GetCustomAttribute<ParameterSourceAttribute>(true);

                string from;

                //if source is specified, set from to specified parameter name.
                //but for Body types, there is no parameter name but serialization method.
                if (attr != null)
                {
                    if (attr.Source == ParameterSource.Body)
                        from = ((FromBodyAttribute) attr).Type.ToString().ToLower();
                    else
                        from = string.IsNullOrEmpty(attr.Name) ? parameter.Name : attr.Name;
                }
                else
                    from = parameter.Name;

                ActionParameter item = new ActionParameter
                                       {
                                           ParameterType = parameter.ParameterType,
                                           ParameterName = parameter.Name,
                                           FromName = from,
                                           Index = i,
                                           Source = attr != null ? attr.Source : ParameterSource.None,
                                           Nullable = Nullable.GetUnderlyingType(parameter.ParameterType) != null
                                       };

                result[i] = item;
            }

            return result;
        }
    }
}