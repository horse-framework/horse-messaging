using System.Threading.Tasks;

namespace Twino.Mvc.Filters
{
    /// <summary>
    /// Filter interface for Controller classes
    /// </summary>
    public interface IBeforeControllerFilter
    {
        /// <summary>
        /// Called BEFORE controller instance created.
        /// If result will be set in this method, action execution and all other filter operations will be canceled and result will be written to the response
        /// </summary>
        Task OnBefore(FilterContext context);
    }
}