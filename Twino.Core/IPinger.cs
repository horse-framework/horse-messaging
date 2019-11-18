namespace Twino.Core
{
    public interface IPinger
    {
        void Add(SocketBase socket);
        void Remove(SocketBase socket);

        void Start();
        void Stop();
    }
}