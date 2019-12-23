using Twino.Mvc.Controllers;
using System;
using System.Reflection;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Route information.
    /// Created when MVC is initialized.
    /// Each Route represents an action method in a controller with specified HTTP Method
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Path part list for the root. parts don't include '/' character, they are splitted with '/'
        /// While in matching operation, the request path is splitted by '/' and each item is checked with this array's items.
        /// </summary>
        public RoutePath[] Path { get; set; }

        /// <summary>
        /// HTTP Method
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Controller type
        /// </summary>
        public Type ControllerType { get; set; }

        /// <summary>
        /// Action method
        /// </summary>
        public MethodInfo ActionType { get; set; }

        /// <summary>
        /// Parameter information of action method.
        /// After MVC initialized, parameters are read from reflection with their attributes and kept in this array
        /// with some pre-calculated values.
        /// </summary>
        public ActionParameter[] Parameters { get; set; }
        
    }
}
