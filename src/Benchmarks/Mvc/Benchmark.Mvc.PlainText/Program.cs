using Twino.Mvc;
using Twino.Protocols.Http;
using Twino.Server;

namespace Benchmark.Mvc.PlainText
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoMvc mvc = new TwinoMvc();
            TwinoServer server = new TwinoServer(ServerOptions.CreateDefault());
            mvc.Init();
            server.UseMvc(mvc, HttpOptions.CreateDefault());
            server.Start();
            server.BlockWhileRunning();
        }
    }
}