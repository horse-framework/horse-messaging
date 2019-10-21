using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the Query String
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : ParameterSourceAttribute
    {
        public FromQueryAttribute() : this(null)
        {
        }

        public FromQueryAttribute(string name): base(name)
        {
            Source = ParameterSource.QueryString;
        }
    }
}
