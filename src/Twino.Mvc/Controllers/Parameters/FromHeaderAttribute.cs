using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the HTTP header
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : ParameterSourceAttribute
    {
        public FromHeaderAttribute() : this(null)
        {
        }

        public FromHeaderAttribute(string name): base(name)
        {
            Source = ParameterSource.Header;
        }
    }
}
