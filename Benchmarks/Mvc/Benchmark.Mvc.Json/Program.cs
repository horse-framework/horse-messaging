using Twino.Mvc;
using Twino.Server;

namespace Benchmark.Mvc.Json
{
    class Program
    {
        static void Main(string[] args)
        {
            using TwinoMvc mvc = new TwinoMvc(ServerOptions.CreateDefault());
            mvc.Init();
            mvc.Run();
        }
    }
}