namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Route part types
    /// </summary>
    public enum RouteType
    {
        /// <summary>
        /// Plain text route part like "v2", "api/test"
        /// </summary>
        Text,

        /// <summary>
        /// When route type is parameter, part value is "{paramName}".
        /// It's checked if exists and sets the method parameter from the route.
        /// </summary>
        Parameter,

        /// <summary>
        /// When route type is optional parameter, part value is "{?paramName}".
        /// This works like Parameter Type but it can be optional and while route matching if this part could not found route can be matched anyway.
        /// </summary>
        OptionalParameter
    }

    /// <summary>
    /// Route path
    /// </summary>
    public class RoutePath
    {
        /// <summary>
        /// Path Type
        /// </summary>
        public RouteType Type { get; set; }

        /// <summary>
        /// Path value such as "api/test", "[controller]", "{paramName}" etc.
        /// </summary>
        public string Value { get; set; }

        public RoutePath()
        {
        }

        public RoutePath(RouteType type, string value)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return "[" + Type + "] " + Value;
        }
    }
}