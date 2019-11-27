using Twino.Mvc.Controllers;
using Twino.Mvc.Filters;
using System;
using Twino.Mvc;

namespace Sample.Mvc.Models
{
    public class TestActionFilterAttribute : Attribute, IActionFilter
    {

        public void After(IController controller, ActionDescriptor descriptor, IActionResult result, FilterContext context)
        {
            Console.WriteLine("After: " + context.Request.Path);
        }

        public void Before(IController controller, ActionDescriptor descriptor, FilterContext context)
        {
            Console.WriteLine("Before: " + context.Request.Path);
            //context.Result = StatusCodeResult.Unauthorized();
        }

    }
}
