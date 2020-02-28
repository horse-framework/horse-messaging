namespace Sample.Mvc.Models
{
    public class SecondService : ISecondService
    {
        private IScopedService _scoped;

        public SecondService(IScopedService scoped)
        {
            _scoped = scoped;
        }
    }
}