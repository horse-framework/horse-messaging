using System;

namespace Twino.Mvc.Middlewares
{
    /// <summary>
    /// Descriptor class for middleware information
    /// </summary>
    internal class MiddlewareDescriptor
    {
        /// <summary>
        /// Not null, If middleware is singleton.
        /// </summary>
        public IMiddleware Instance { get; set; }

        /// <summary>
        /// Middleware object type
        /// </summary>
        public Type MiddlewareType { get; set; }

        /// <summary>
        /// Middleware's constructor parameter types
        /// </summary>
        public Type[] ConstructorParameters { get; set; }

    }
}