using Twino.Mvc.Controllers;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Filter interface for Controller classes
    /// </summary>
    public interface IControllerFilter
    {

        /// <summary>
        /// Called BEFORE controller instance created.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        void BeforeCreated(FilterContext context);

        /// <summary>
        /// Called AFTER controller instance created.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        void AfterCreated(IController controller, FilterContext context);

        /// <summary>
        /// Called BEFORE action method executed and BEFORE IActionFilter objects' Before methods are called.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        void BeforeAction(IController controller, ActionDescriptor descriptor, FilterContext context);

        /// <summary>
        /// Called AFTER action method executed and AFTER IActionFilter objects' After methods are called.
        /// Changing Result of the FilterContext will be discarded.
        /// </summary>
        void AfterAction(IController controller, ActionDescriptor descriptor, IActionResult result, FilterContext context);

    }
}
