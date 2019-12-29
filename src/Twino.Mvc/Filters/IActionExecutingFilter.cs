using System.Threading.Tasks;
using Twino.Mvc.Controllers;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Filter interface for Controller and Action Methods
    /// </summary>
    public interface IActionExecutingFilter
    {
        /// <summary>
        /// Called BEFORE action method executed and AFTER IControllerFilter objects' BeforeAction methods are called.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        Task OnExecuting(IController controller, ActionDescriptor descriptor, FilterContext context);
    }
}