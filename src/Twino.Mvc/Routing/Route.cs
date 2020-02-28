using System;
using System.Reflection;
using Twino.Mvc.Controllers;

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

        /// <summary>
        /// If true, when route has Authorize attribute 
        /// </summary>
        internal bool HasControllerAuthorizeFilter { get; set; }

        /// <summary>
        /// If true, when route has IBeforeControllerFilter 
        /// </summary>
        internal bool HasControllerBeforeFilter { get; set; }

        /// <summary>
        /// If true, when route has IAfterControllerFilter 
        /// </summary>
        internal bool HasControllerAfterFilter { get; set; }

        /// <summary>
        /// If true, when controller has IActionExecutingFilter 
        /// </summary>
        internal bool HasControllerExecutingFilter { get; set; }

        /// <summary>
        /// If true, when controller has IActionExecutedFilter 
        /// </summary>
        internal bool HasControllerExecutedFilter { get; set; }

        /// <summary>
        /// If true, when route has Authorize attribute 
        /// </summary>
        internal bool HasActionAuthorizeFilter { get; set; }

        /// <summary>
        /// If true, when action method has IActionExecutingFilter 
        /// </summary>
        internal bool HasActionExecutingFilter { get; set; }

        /// <summary>
        /// If true, when action method has IActionExecutedFilter 
        /// </summary>
        internal bool HasActionExecutedFilter { get; set; }

        /// <summary>
        /// If true, method return type is generic Task.
        /// If false, method return type is IActionResult.
        /// </summary>
        internal bool IsAsyncMethod { get; set; }
    }
}