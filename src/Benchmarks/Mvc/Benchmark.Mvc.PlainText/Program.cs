using Twino.Mvc;
using Twino.Server;

namespace Benchmark.Mvc.PlainText
{
    class Program
    {
        static void Main(string[] args)
        {
            TwinoMvc mvc = new TwinoMvc();
            TwinoServer server = new TwinoServer();
            mvc.Init();
            server.UseMvc(mvc);
            server.Start(5000);
            server.BlockWhileRunning();
        }
    }
}