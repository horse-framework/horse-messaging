using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the content form
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromFormAttribute : ParameterSourceAttribute
    {
        /// <summary>
        /// Creates new from from attribute
        /// </summary>
        public FromFormAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new from form attribute with form name
        /// </summary>
        /// <param name="name"></param>
        public FromFormAttribute(string name) : base(name)
        {
            Source = ParameterSource.Form;
        }
    }
}
