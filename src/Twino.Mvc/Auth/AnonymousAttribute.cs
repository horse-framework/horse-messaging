using System;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// Authorization attribute for Twino MVC.
    /// Can be used for controllers and actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AllowAnonymousAttribute : Attribute
    { }
}
