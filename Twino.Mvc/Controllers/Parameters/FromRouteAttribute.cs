using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the Route
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromRouteAttribute : ParameterSourceAttribute
    {
        public FromRouteAttribute() : this(null)
        {
        }

        public FromRouteAttribute(string name) : base(name)
        {
            Source = ParameterSource.Route;
        }
    }
}
