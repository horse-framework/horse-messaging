using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Attribute for parameters will be read from the content form
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromFormAttribute : ParameterSourceAttribute
    {
        public FromFormAttribute() : this(null)
        {
        }

        public FromFormAttribute(string name) : base(name)
        {
            Source = ParameterSource.Form;
        }
    }
}
