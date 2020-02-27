using System;
using System.Threading.Tasks;
using Twino.Mvc;
using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;

namespace Sample.Mvc.Models
{
    public class TestActionFilterAttribute : Attribute, IActionExecutingFilter, IActionExecutedFilter
    {
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
