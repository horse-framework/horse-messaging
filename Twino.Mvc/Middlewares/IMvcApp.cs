namespace Twino.Mvc.Middlewares
{
    public interface IMvcApp
    {
        void UseMiddleware(IMiddleware middleware);
    }
}
