using Twino.Mvc.Filters;
using System.Collections.Generic;
using System.Reflection;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Detailed information for the specified action method of a request
    /// </summary>
    public class ActionDescriptor
    {
        /// <summary>
        /// Controller of the action method.
        /// The Action method is a member of that controller.
        /// </summary>
        public IController Controller { get; set; }

        /// <summary>
        /// Reflection info for the specified action method
        /// </summary>
        public MethodInfo Action { get; set; }

        /// <summary>
        /// All parameters with values of the action method
        /// </summary>
        public IEnumerable<ParameterValue> Parameters { get; set; }
    }
}