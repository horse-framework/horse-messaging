using System;

namespace Sample.Mvc.Models
{
    public class ScopedService : IScopedService
    {
        public ScopedService()
        {
            Console.WriteLine("Scoped service is created");
        }
    }
}