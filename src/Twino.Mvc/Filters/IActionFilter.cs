using Twino.Mvc.Controllers;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Filter interface for Controller Action Methods
    /// </summary>
    public interface IActionFilter
    {
        /// <summary>
        /// Called BEFORE action method executed and AFTER IControllerFilter objects' BeforeAction methods are called.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        void Before(IController controller, ActionDescriptor descriptor, FilterContext context);

        /// <summary>
        /// Called AFTER action method executed and BEFORE IControllerFilter objects' AfterAction methods are called.
        /// Changing Result of the FilterContext will be discarded.
        /// </summary>
        void After(IController controller, ActionDescriptor descriptor, IActionResult result, FilterContext context);

    }
}
