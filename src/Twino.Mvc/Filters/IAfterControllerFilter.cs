using System.Threading.Tasks;
using Twino.Mvc.Controllers;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Filter interface for Controller classes
    /// </summary>
    public interface IAfterControllerFilter
    {
        /// <summary>
        /// Called AFTER controller instance created.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        Task OnAfter(IController controller, FilterContext context);
    }
}