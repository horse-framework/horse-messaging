using System.Threading.Tasks;
using Test.Ioc.Services;
using Twino.Ioc;

namespace Playground
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            services.CheckServices();
        }
    }
}