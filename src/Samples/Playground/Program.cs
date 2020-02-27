using System;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Ioc;

namespace Playground
{

    interface IService1
    {
        void Test1();
    }

    interface IService2
    {
        void Test2();
    }

    class Service1 : IService1
    {

        public void Test1()
        {
            Console.WriteLine("echo 1");
        }
    }

    class Service2 : IService2
    {
        public void Test2()
        { }
    }

    interface IService3 { }
    class Service3 : IService3
    {
        public Service3(IService1 service1)
        {
            service1.Test1();
        }
    }

    class Service1Proxy : IServiceProxy
    {
        private IService2 _service2;
        public Service1Proxy(IService2 service2)
        {
            _service2 = service2;
        }

        public object Proxy(object decorated)
        {
            return DenemeDispatchProxy<IService1>.Create((IService1)decorated, _service2);
        }
    }


    class DenemeDispatchProxy<T> : DispatchProxy
    {
        private T _decorated;
        private IService2 _service2;
        public static T Create(T decorated, IService2 service2)
        {
            object proxy = Create<T, DenemeDispatchProxy<T>>();
            DenemeDispatchProxy<T> instance = (DenemeDispatchProxy<T>)proxy;
            instance._decorated = decorated;
            instance._service2 = service2;
            return (T)proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Console.WriteLine("PROXY");
            return targetMethod.Invoke(_decorated, args);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var container = new ServiceContainer();
            container.AddSingleton<IService1, Service1, Service1Proxy>();
            container.AddSingleton<IService2, Service2>();
            container.AddSingleton<IService3, Service3>();
            var instance3 = await container.Get<IService3>(container.CreateScope());
            Console.ReadLine();
        }
    }
}