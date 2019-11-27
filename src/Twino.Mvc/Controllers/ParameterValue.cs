using System;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Parameter value information for the ActionParameter object.
    /// ActionParameter objects are created on init and they contains all information but not value.
    /// ParameterValue is created for representing these ActionParameter objects for each request and they contains values.
    /// </summary>
    public class ParameterValue
    {
        /// <summary>
        /// Action Method parameter name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Action method parameter value
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Parameter request source
        /// </summary>
        public ParameterSource Source { get; set; }

        /// <summary>
        /// Action Method parameter type
        /// </summary>
        public Type Type { get; set; }
    }
}
