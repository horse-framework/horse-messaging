using System;

namespace Sample.Mvc.Models
{
    public class DemoService : IDemoService
    {
        private Random rnd = new Random();

        public string Name = "A";

        public int GetNumber()
        {
            return rnd.Next(0, 1000);
        }
    }
}
