using System;
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
        public async Task TransientInTransientPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransientPool<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p.Foo);
            Assert.NotEqual(parent.First.Foo, p.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p.Second.Foo);

            IFirstChildService f1 = await services.Get<IFirstChildService>();
            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            Assert.NotEqual(parent.First.Foo, f1.Foo);
            Assert.NotEqual(parent.First.Foo, f2.Foo);

            ISecondChildService s1 = await services.Get<ISecondChildService>();
            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            Assert.NotEqual(parent.Second.Foo, s1.Foo);
            Assert.NotEqual(parent.Second.Foo, s2.Foo);
        }

        [Fact]
        public async Task ScopedInTransient()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IParentService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IFirstChildService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p1 = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p1.Foo);
            Assert.Equal(parent.First.Foo, p1.First.Foo);
            Assert.Equal(parent.Second.Foo, p1.Second.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.NotEqual(parent.First.Foo, p2.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p2.Second.Foo);
        }

        [Fact]
        public async Task ScopedInSingleton()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddSingleton<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IParentService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IFirstChildService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p1 = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p1.Foo);
            Assert.Equal(parent.First.Foo, p1.First.Foo);
            Assert.Equal(parent.Second.Foo, p1.Second.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.Equal(parent.Foo, p2.Foo);
            Assert.NotEqual(parent.First.Foo, p2.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p2.Second.Foo);
        }

        [Fact]
        public async Task ScopedInTransientPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransientPool<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IFirstChildService>());
            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            Assert.Equal(parent.First.Foo, f2.Foo);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());
            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            Assert.Equal(parent.Second.Foo, s2.Foo);
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