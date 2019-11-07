using System.Reflection;
using System.Threading.Tasks;
using Test.Ioc.Services;
using Twino.Ioc;
using Xunit;

namespace Test.Ioc
{
    public class MixedTest
    {
        [Fact]
        public async Task TransientInScoped()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p.Foo);
            Assert.NotEqual(parent.First.Foo, p.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p.Second.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>(scope);
            Assert.NotEqual(parent.First.Foo, first.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>(scope);
            Assert.NotEqual(parent.Second.Foo, second.Foo);
        }

        [Fact]
        public async Task TransientInSingleton()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddSingleton<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            IParentService parent = await services.Get<IParentService>();
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>();
            Assert.Equal(parent.Foo, p.Foo);
            Assert.NotEqual(parent.First.Foo, p.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p.Second.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>();
            Assert.NotEqual(parent.First.Foo, first.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>();
            Assert.NotEqual(parent.Second.Foo, second.Foo);
        }

        [Fact]
        public async Task TransientInPool()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task ScopedInTransient()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task ScopedInSingleton()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task ScopedInPool()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task SingletonInTransient()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task SingletonInScoped()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task SingletonInPool()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task PoolInTransient()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task PoolInScoped()
        {
            ServiceContainer services = new ServiceContainer();
        }

        [Fact]
        public async Task PoolInSingleton()
        {
            ServiceContainer services = new ServiceContainer();
        }
    }
}