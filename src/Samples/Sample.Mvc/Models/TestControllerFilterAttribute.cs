using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using System;
using Twino.Mvc;

namespace Sample.Mvc.Models
{
    public class TestControllerFilterAttribute : Attribute, IControllerFilter
    {
        public void AfterAction(IController controller, ActionDescriptor descriptor, IActionResult result, FilterContext context)
        {
            Console.WriteLine("After Action: " + context.Request.Path);
        }

        public void AfterCreated(IController controller, FilterContext context)
        {
            Console.WriteLine("After Created: " + context.Request.Path);
        }

        public void BeforeAction(IController controller, ActionDescriptor descriptor, FilterContext context)
        {
            Console.WriteLine("Before Action: " + context.Request.Path);
        }

        public void BeforeCreated(FilterContext context)
        {
            Console.WriteLine("Before Created: " + context.Request.Path);
        }
    }
}
