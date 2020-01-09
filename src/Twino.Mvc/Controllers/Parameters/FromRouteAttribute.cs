using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the Route
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromRouteAttribute : ParameterSourceAttribute
    {
        /// <summary>
        /// Creates from route attribute with parameter name
        /// </summary>
        public FromRouteAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates from route attribute with specified name
        /// </summary>
        public FromRouteAttribute(string name) : base(name)
        {
            Source = ParameterSource.Route;
        }
    }
}
