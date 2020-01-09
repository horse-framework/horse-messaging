using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the HTTP header
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : ParameterSourceAttribute
    {
        /// <summary>
        /// Creates from header attribute with parameter name
        /// </summary>
        public FromHeaderAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates from header attribute with specified name
        /// </summary>
        public FromHeaderAttribute(string name): base(name)
        {
            Source = ParameterSource.Header;
        }
    }
}
