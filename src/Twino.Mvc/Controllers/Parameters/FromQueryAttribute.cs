using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the Query String
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : ParameterSourceAttribute
    {
        /// <summary>
        /// Creates from query attribute with parameter name
        /// </summary>
        public FromQueryAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates from query attribute with specified name
        /// </summary>
        public FromQueryAttribute(string name) : base(name)
        {
            Source = ParameterSource.QueryString;
        }
    }
}
