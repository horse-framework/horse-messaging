using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute to specify action method parameter sources.
    /// This attribute can be used for only method parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ParameterSourceAttribute : Attribute
    {
        /// <summary>
        /// Source of the parameter
        /// </summary>
        public ParameterSource Source { get; protected set; }

        /// <summary>
        /// Parameter's source name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates source attribute, used by FromBody, FromHeader etc. attributes
        /// </summary>
        public ParameterSourceAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates source attribute, used by FromBody, FromHeader etc. attributes
        /// </summary>
        public ParameterSourceAttribute(string name)
        {
            Name = name;
            Source = ParameterSource.Header;
        }
    }
}