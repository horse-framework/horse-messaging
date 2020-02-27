using System;
using System.Linq;
using System.Threading.Tasks;
using Twino.Ioc;
using Twino.MQ.Data;
using Twino.Protocols.TMQ;

namespace Playground
{
    public interface ITestService
    {
        void Test();
    }

    public class TestService : ITestService
    {

        private readonly IDenemeInjector denemeInjector;

        public TestService(IDenemeInjector inj)
        {
            denemeInjector = inj;
            denemeInjector.Inject();
        }

        public void Test()
        {
            Console.WriteLine("Emre");
        }
    }

    public class TestProxy : ITestService
    {
        public void Test()
        {
            Console.WriteLine("Emre 2");
        }
    }

    public class TestServiceDecorator : IServiceProxy
    {
        public TestServiceDecorator(IDenemeInjector inj)
        {
            inj.Inject();
        }

        public object Proxy(object decorated)
        {
            return (ITestService)new TestProxy();
        }
    }

    public interface IDenemeInjector
    {
        void Inject();
    }

    public class DenemeInjector : IDenemeInjector
    {
        public void Inject()
        {
            Console.WriteLine("Injectli la bu");
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var container = new ServiceContainer();
            container.AddTransient<ITestService, TestService, TestServiceDecorator>();
            container.AddTransient<IDenemeInjector, DenemeInjector>();
            var instance = await container.Get<ITestService>();
            instance.Test();
            Console.ReadLine();
        }
    }
}