namespace Sample.Mvc.Models
{
    public class FirstService : IFirstService
    {
        private IScopedService _scoped;

        public FirstService(IScopedService scoped)
        {
            _scoped = scoped;
        }
    }
}