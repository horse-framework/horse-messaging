using System;
using System.Collections.Generic;
using System.Linq;
using Twino.Mvc.Middlewares;
using Twino.Mvc.Routing;

namespace Twino.Mvc.Explorer
{
    public static class ExplorerExtensions
    {
        
        public static TwinoMvc AddExplorer(this TwinoMvc mvc)
        {
            Dictionary<Type, Route[]> routes = mvc.Routes
                                                  .GroupBy(x => x.ControllerType)
                                                  .ToDictionary(x => x.Key,
                                                                x => x.ToArray());

            
            return mvc;
        }

        public static IMvcAppBuilder UseExplorer(this IMvcAppBuilder app)
        {
            return app;
        }
    }
}