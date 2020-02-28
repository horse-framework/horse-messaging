using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Controllers.Parameters;
using Twino.Mvc.Filters;
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
        private IEnumerable<RouteLeaf> GetEdgeLeaves(RouteLeaf root)
        {
            if (root.Children.Count == 0)
            {
                yield return root;
                yield break;
            }

            foreach (RouteLeaf leaf in root.Children)
            {
                IEnumerable<RouteLeaf> leaves = GetEdgeLeaves(leaf);
                foreach (RouteLeaf child in leaves)
                    yield return child;
            }
        }

        /// <summary>
        /// Creates all Route objects of the specified Controller type
        /// </summary>
        public IEnumerable<RouteLeaf> BuildRoutes(Type controllerType)
        {
            //find base path for the controller
            List<RouteLeaf> routes = CreateControllerRoutes(controllerType);
            List<RouteLeaf> edges = new List<RouteLeaf>();
            foreach (RouteLeaf leaf in routes)
                edges.AddRange(GetEdgeLeaves(leaf));

            MethodInfo[] methods = controllerType.GetMethods();
            foreach (MethodInfo method in methods)
            {
                //find method route if exists
                List<RouteInfo> methodRoutes = FindActionRoute(method);
                if (methodRoutes.Count == 0)
                    continue;

                foreach (RouteLeaf cleaf in edges)
                {
                    foreach (RouteInfo info in methodRoutes)
                    {
                        //get route table from the fullpath
                        List<RoutePath> path = GetRoutePath(method, info.Pattern);

                        RouteLeaf leaf = cleaf;
                        for (int i = 0; i < path.Count; i++)
                        {
                            RoutePath rp = path[i];
                            RouteLeaf child = new RouteLeaf(rp, leaf);
                            leaf.Children.Add(child);
                            leaf = child;

                            if (i == path.Count - 1)
                            {
                                leaf.Route = new Route
                                             {
                                                 ActionType = method,
                                                 ControllerType = controllerType,
                                                 Method = info.Method,
                                                 Path = path.ToArray(),
                                                 Parameters = BuildParameters(method),
                                                 IsAsyncMethod = (AsyncStateMachineAttribute) method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null
                                             };
                            }
                        }

                        ApplyControllerAttributes(leaf.Route, controllerType);
                        ApplyActionAttributes(leaf.Route, method);
                    }
                }
            }

            return routes;
        }

        /// <summary>
        /// Sorts all route leaves recursively
        /// </summary>
        internal void SortRoutes(RouteLeaf leaf)
        {
            if (leaf.Children.Count == 0)
                return;
            
            leaf.Children.Sort((a, b) =>
            {
                if (string.IsNullOrEmpty(a.Path.Value))
                    return 1;

                if (string.IsNullOrEmpty(b.Path.Value))
                    return -1;

                return 0;
            });

            foreach (RouteLeaf child in leaf.Children)
                SortRoutes(child);
        }

        /// <summary>
        /// Searches method attributes and applies existing attributes to route
        /// </summary>
        private static void ApplyControllerAttributes(Route route, Type controller)
        {
            foreach (CustomAttributeData data in controller.GetCustomAttributesData())
            {
                if (typeof(AuthorizeAttribute).IsAssignableFrom(data.AttributeType))
                    route.HasControllerAuthorizeFilter = true;

                if (typeof(IBeforeControllerFilter).IsAssignableFrom(data.AttributeType))
                    route.HasControllerBeforeFilter = true;

                if (typeof(IAfterControllerFilter).IsAssignableFrom(data.AttributeType))
                    route.HasControllerAfterFilter = true;

                if (typeof(IActionExecutingFilter).IsAssignableFrom(data.AttributeType))
                    route.HasControllerExecutingFilter = true;

                if (typeof(IActionExecutedFilter).IsAssignableFrom(data.AttributeType))
                    route.HasControllerExecutedFilter = true;
            }
        }

        /// <summary>
        /// Searches method attributes and applies existing attributes to route
        /// </summary>
        private static void ApplyActionAttributes(Route route, MethodInfo method)
        {
            foreach (CustomAttributeData data in method.GetCustomAttributesData())
            {
                if (typeof(AuthorizeAttribute).IsAssignableFrom(data.AttributeType))
                    route.HasActionAuthorizeFilter = true;

                if (typeof(IActionExecutingFilter).IsAssignableFrom(data.AttributeType))
                    route.HasActionExecutingFilter = true;

                if (typeof(IActionExecutedFilter).IsAssignableFrom(data.AttributeType))
                    route.HasActionExecutedFilter = true;
            }
        }

        /// <summary>
        /// Finds route value for the controller type
        /// </summary>
        private static List<RouteLeaf> CreateControllerRoutes(Type controllerType)
        {
            List<RouteLeaf> routes = new List<RouteLeaf>();
            IEnumerable<RouteAttribute> attributes = controllerType.GetCustomAttributes<RouteAttribute>(true);

            foreach (RouteAttribute attr in attributes)
            {
                string value = attr.Pattern;
                if (value == null)
                {
                    routes.Add(new RouteLeaf(new RoutePath(RouteType.Text, FindControllerRouteName(controllerType)), null));
                    continue;
                }

                if (value == "")
                {
                    routes.Add(new RouteLeaf(new RoutePath(RouteType.Text, ""), null));
                    continue;
                }

                string[] pathLeaves = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
                RouteLeaf parent = null;
                foreach (string p in pathLeaves)
                {
                    string v = p;
                    if (p.Equals("[controller]", StringComparison.InvariantCultureIgnoreCase))
                        v = FindControllerRouteName(controllerType);

                    RouteLeaf leaf = new RouteLeaf(new RoutePath(RouteType.Text, v), parent);
                    if (parent == null)
                        routes.Add(leaf);
                    else
                        parent.Children.Add(leaf);

                    parent = leaf;
                }
            }

            //if there is no route definition, the route value is the name of the controller
            if (routes.Count == 0)
                routes.Add(new RouteLeaf(new RoutePath(RouteType.Text, FindControllerRouteName(controllerType)), null));

            return routes;
        }

        /// <summary>
        /// Finds route name of controller type
        /// </summary>
        private static string FindControllerRouteName(Type type)
        {
            string value = type.Name.ToLower(new CultureInfo("en-US"));

            //remove value from the end
            if (value.EndsWith("controller"))
                value = value.Substring(0, value.Length - "controller".Length);

            return value;
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

                string pattern = attr.Pattern ?? "";
                RouteInfo route = new RouteInfo
                                  {
                                      Method = attr.Method,
                                      Pattern = pattern,
                                      Path = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                  };

                routes.Add(route);
            }

            return routes;
        }

        /// <summary>
        /// Creates RoutePath table from the controller type, action method reflection info and full path.
        /// </summary>
        private List<RoutePath> GetRoutePath(MethodInfo action, string fullpath)
        {
            List<RoutePath> result = new List<RoutePath>();

            if (string.IsNullOrEmpty(fullpath))
            {
                result.Add(new RoutePath
                           {
                               Type = RouteType.Text,
                               Value = ""
                           });

                return result;
            }

            //split path to parts.
            //each part will be replaced to RoutePath class with extra information
            string[] parts = fullpath.ToLower(new CultureInfo("en-US")).Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                RoutePath route = new RoutePath();

                if (part == "[action]")
                {
                    route.Type = RouteType.Text;
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
                                           Nullable = Nullable.GetUnderlyingType(parameter.ParameterType) != null,
                                           IsClass = parameter.ParameterType.IsClass && parameter.ParameterType != typeof(string)
                                       };

                if (item.IsClass)
                {
                    PropertyInfo[] properties = parameter.ParameterType.GetProperties();
                    foreach (PropertyInfo property in properties)
                        item.ClassProperties.Add(property.Name, property);
                }

                result[i] = item;
            }

            return result;
        }
    }
}