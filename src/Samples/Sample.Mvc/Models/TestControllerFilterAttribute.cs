using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using System;
using System.Threading.Tasks;
using Twino.Mvc;

namespace Sample.Mvc.Models
{
    public class TestControllerFilterAttribute : Attribute, IBeforeControllerFilter, IAfterControllerFilter, IActionExecutingFilter, IActionExecutedFilter
    {
        public Task OnBefore(FilterContext context)
        {
            Console.WriteLine("OnBefore: " + context.Request.Path);
            return Task.CompletedTask;
        }

        public Task OnAfter(IController controller, FilterContext context)
        {
            Console.WriteLine("OnAfter: " + context.Request.Path);
            return Task.CompletedTask;
        }

        public Task OnExecuting(IController controller, ActionDescriptor descriptor, FilterContext context)
        {
            Console.WriteLine("OnExecuting: " + context.Request.Path);
            return Task.CompletedTask;
        }

        public Task OnExecuted(IController controller, ActionDescriptor descriptor, IActionResult result, FilterContext context)
        {
            Console.WriteLine("OnExecuted: " + context.Request.Path);
            return Task.CompletedTask;
        }
    }
}
