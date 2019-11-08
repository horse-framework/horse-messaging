using System;
using System.Threading.Tasks;
using Test.Ioc.Services;
using Twino.Ioc;
using Xunit;

namespace Test.Ioc
{
    public class MixedTest
    {
        #region Transient

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

            //services in scoped service is created only once (cuz they created via parent)
            IParentService p = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

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

            //services in singleton service is created only once (cuz they created via parent)
            IParentService p = await services.Get<IParentService>();
            Assert.Equal(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

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
        public async Task TransientInScopedPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScopedPool<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            //we are getting same instance of parent. so, children are same.
            IParentService p = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

            //we are getting individual children, so they are created new and different
            IFirstChildService f1 = await services.Get<IFirstChildService>();
            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            Assert.NotEqual(parent.First.Foo, f1.Foo);
            Assert.NotEqual(parent.First.Foo, f2.Foo);

            //we are getting individual children, so they are created new and different
            ISecondChildService s1 = await services.Get<ISecondChildService>();
            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            Assert.NotEqual(parent.Second.Foo, s1.Foo);
            Assert.NotEqual(parent.Second.Foo, s2.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p2.Foo);
        }

        #endregion

        #region Scoped

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

            //in same scope, individual scoped items should equal
            IFirstChildService f1 = await services.Get<IFirstChildService>(scope);
            ISecondChildService s1 = await services.Get<ISecondChildService>(scope);
            Assert.Equal(parent.First.Foo, f1.Foo);
            Assert.Equal(parent.Second.Foo, s1.Foo);

            //scoped services in singleton should equal (because parent is same)
            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.Equal(parent.Foo, p2.Foo);
            Assert.Equal(parent.First.Foo, p2.First.Foo);
            Assert.Equal(parent.Second.Foo, p2.Second.Foo);

            //but individual created scoped items in different scope should not equal
            IFirstChildService f2 = await services.Get<IFirstChildService>(scope2);
            ISecondChildService s2 = await services.Get<ISecondChildService>(scope2);
            Assert.NotEqual(parent.First.Foo, f2.Foo);
            Assert.NotEqual(parent.Second.Foo, s2.Foo);
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
        public async Task ScopedInScopedPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScopedPool<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IFirstChildService>());
            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            Assert.Equal(parent.First.Foo, f2.Foo);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());
            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            Assert.Equal(parent.Second.Foo, s2.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.NotEqual(parent.First.Foo, p2.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p2.Second.Foo);
        }

        #endregion

        #region Singleton

        [Fact]
        public async Task SingletonInTransient()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<IParentService, ParentService>();
            services.AddSingleton<IFirstChildService, FirstChildService>();
            services.AddSingleton<ISecondChildService, SecondChildService>();

            IFirstChildService first = await services.Get<IFirstChildService>();
            first.Foo = "first";

            IParentService parent = await services.Get<IParentService>();
            parent.Foo = "parent";
            parent.Second.Foo = "second";
            Assert.Equal(first.Foo, parent.First.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>();
            Assert.Equal(second.Foo, parent.Second.Foo);

            IParentService p = await services.Get<IParentService>();
            Assert.NotEqual(p.Foo, parent.Foo);
            Assert.Equal(p.First.Foo, parent.First.Foo);
            Assert.Equal(p.Second.Foo, parent.Second.Foo);
        }

        [Fact]
        public async Task SingletonInScoped()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<IParentService, ParentService>();
            services.AddSingleton<IFirstChildService, FirstChildService>();
            services.AddSingleton<ISecondChildService, SecondChildService>();
            IContainerScope scope = services.CreateScope();

            IFirstChildService first = await services.Get<IFirstChildService>(scope);
            first.Foo = "first";

            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.Second.Foo = "second";
            Assert.Equal(first.Foo, parent.First.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>(scope);
            Assert.Equal(second.Foo, parent.Second.Foo);

            IParentService p1 = await services.Get<IParentService>(scope);
            Assert.Equal(p1.Foo, parent.Foo);
            Assert.Equal(p1.First.Foo, parent.First.Foo);
            Assert.Equal(p1.Second.Foo, parent.Second.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(p2.Foo, parent.Foo);
            Assert.Equal(p2.First.Foo, parent.First.Foo);
            Assert.Equal(p2.Second.Foo, parent.Second.Foo);
        }

        [Fact]
        public async Task SingletonInTransientPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransientPool<IParentService, ParentService>();
            services.AddSingleton<IFirstChildService, FirstChildService>();
            services.AddSingleton<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            IFirstChildService f3 = await services.Get<IFirstChildService>();
            Assert.Equal(parent.First.Foo, f2.Foo);
            Assert.Equal(parent.First.Foo, f3.Foo);

            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            ISecondChildService s3 = await services.Get<ISecondChildService>();
            Assert.Equal(parent.Second.Foo, s2.Foo);
            Assert.Equal(parent.Second.Foo, s3.Foo);

            IParentService p2 = await services.Get<IParentService>();
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.Equal(parent.First.Foo, p2.First.Foo);
            Assert.Equal(parent.Second.Foo, p2.Second.Foo);
        }

        [Fact]
        public async Task SingletonInScopedPool()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScopedPool<IParentService, ParentService>();
            services.AddSingleton<IFirstChildService, FirstChildService>();
            services.AddSingleton<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();
            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p.Foo);
            Assert.Equal(parent.First.Foo, p.First.Foo);
            Assert.Equal(parent.Second.Foo, p.Second.Foo);

            IFirstChildService f2 = await services.Get<IFirstChildService>(scope);
            IFirstChildService f3 = await services.Get<IFirstChildService>();
            Assert.Equal(parent.First.Foo, f2.Foo);
            Assert.Equal(parent.First.Foo, f3.Foo);

            ISecondChildService s2 = await services.Get<ISecondChildService>(scope);
            ISecondChildService s3 = await services.Get<ISecondChildService>();
            Assert.Equal(parent.Second.Foo, s2.Foo);
            Assert.Equal(parent.Second.Foo, s3.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.Equal(parent.First.Foo, p2.First.Foo);
            Assert.Equal(parent.Second.Foo, p2.Second.Foo);
        }

        #endregion

        #region Pool

        [Fact]
        public async Task PoolsInTransient()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<IParentService, ParentService>();
            services.AddTransientPool<IFirstChildService, FirstChildService>();
            services.AddScopedPool<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IParentService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());

            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p1 = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p1.Foo);
            Assert.NotEqual(parent.First.Foo, p1.First.Foo);
            Assert.Equal(parent.Second.Foo, p1.Second.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>(scope);
            IFirstChildService f2 = await services.Get<IFirstChildService>();
            Assert.NotEqual(parent.First.Foo, first.Foo);
            Assert.NotEqual(parent.First.Foo, f2.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>(scope);
            Assert.Equal(parent.Second.Foo, second.Foo);

            IContainerScope scope2 = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.NotEqual(parent.First.Foo, p2.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p2.Second.Foo);
        }

        [Fact]
        public async Task PoolsInScoped()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<IParentService, ParentService>();
            services.AddTransientPool<IFirstChildService, FirstChildService>();
            services.AddScopedPool<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();

            throw new NotImplementedException();
        }

        [Fact]
        public async Task PoolsInSingleton()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddSingleton<IParentService, ParentService>();
            services.AddTransientPool<IFirstChildService, FirstChildService>();
            services.AddScopedPool<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();

            throw new NotImplementedException();
        }

        #endregion
    }
}