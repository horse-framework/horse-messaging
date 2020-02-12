using System.Threading.Tasks;

namespace Sample.MQ.Data
{
    class Program
    {
        static async Task Main(string[] args)
        {
            while (true)
            {
                Overload o = new Overload();
                await o.OverloadAsync();   
            }
        }
        
    }
}