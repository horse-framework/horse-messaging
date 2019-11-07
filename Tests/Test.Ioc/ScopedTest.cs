using System;
using Test.Ioc.Services;
using Twino.Ioc;
using Xunit;

namespace Test.Ioc
{
    public class ScopedTest
    {
        [Fact]
        public async void Single()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<ISingleService, SingleService>();

            IContainerScope scope = services.CreateScope();

            ISingleService s1 = await services.Get<ISingleService>(scope);
            s1.Foo = "a";

            ISingleService s2 = await services.Get<ISingleService>(scope);
            Assert.Equal(s1.Foo, s2.Foo);

            //scopeless should throw error
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISingleService>());

            //another scope should not equal
            IContainerScope scope2 = services.CreateScope();
            ISingleService s4 = await services.Get<ISingleService>(scope2);
            Assert.NotEqual(s1.Foo, s4.Foo);
            Assert.NotEqual(s2.Foo, s4.Foo);
        }

        [Fact]
        public async void Nested()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();

            IParentService parent = await services.Get<IParentService>(scope);
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p1 = await services.Get<IParentService>(scope);
            Assert.Equal(parent.Foo, p1.Foo);
            Assert.Equal(parent.First, p1.First);
            Assert.Equal(parent.Second, p1.Second);
            Assert.Equal(parent.First.Foo, p1.First.Foo);
            Assert.Equal(parent.Second.Foo, p1.Second.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>(scope);
            ISecondChildService second = await services.Get<ISecondChildService>(scope);
            Assert.Equal(parent.First, first);
            Assert.Equal(parent.First.Foo, first.Foo);
            Assert.Equal(parent.Second.Foo, second.Foo);

            //scopeless should throw error
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IParentService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<IFirstChildService>());
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<ISecondChildService>());
            
            //another scope should not equal
            IContainerScope scope2 = services.CreateScope();
            IParentService p3 = await services.Get<IParentService>(scope2);
            Assert.NotEqual(parent.Foo, p3.Foo);
            Assert.NotEqual(parent.First, p3.First);
            Assert.NotEqual(parent.Second, p3.Second);
            Assert.NotEqual(parent.First.Foo, p3.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p3.Second.Foo);
        }

        [Fact]
        public async void MultipleNestedDoubleParameter()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddScoped<INestParentService, NestParentService>();
            services.AddScoped<ISingleService, SingleService>();
            services.AddScoped<IParentService, ParentService>();
            services.AddScoped<IFirstChildService, FirstChildService>();
            services.AddScoped<ISecondChildService, SecondChildService>();

            IContainerScope scope = services.CreateScope();

            INestParentService nest = await services.Get<INestParentService>(scope);
            nest.Foo = "nest";
            nest.Parent.Foo = "parent";
            nest.Parent.First.Foo = "first";
            nest.Parent.Second.Foo = "second";
            nest.Single.Foo = "single";

            INestParentService n1 = await services.Get<INestParentService>(scope);
            Assert.Equal(nest.Foo, n1.Foo);
            Assert.Equal(nest.Single.Foo, n1.Single.Foo);
            Assert.Equal(nest.Parent.Foo, n1.Parent.Foo);
            Assert.Equal(nest.Parent.First.Foo, n1.Parent.First.Foo);
            Assert.Equal(nest.Parent.Second.Foo, n1.Parent.Second.Foo);

            IParentService parent = await services.Get<IParentService>(scope);
            Assert.Equal(nest.Parent.Foo, parent.Foo);
            Assert.Equal(nest.Parent.First.Foo, parent.First.Foo);
            Assert.Equal(nest.Parent.Second.Foo, parent.Second.Foo);

            ISingleService single = await services.Get<ISingleService>(scope);
            Assert.Equal(nest.Single.Foo, single.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>(scope);
            Assert.Equal(nest.Parent.First.Foo, first.Foo);

            ISecondChildService second = await services.Get<ISecondChildService>(scope);
            Assert.Equal(nest.Parent.Second.Foo, second.Foo);

            //scopeless should throw error
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await services.Get<INestParentService>());

            //another scope should not equal
            IContainerScope scope2 = services.CreateScope();
            INestParentService n3 = await services.Get<INestParentService>(scope2);
            Assert.NotEqual(nest.Foo, n3.Foo);
            Assert.NotEqual(nest.Single.Foo, n3.Single.Foo);
            Assert.NotEqual(nest.Parent.Foo, n3.Parent.Foo);
            Assert.NotEqual(nest.Parent.First.Foo, n3.Parent.First.Foo);
            Assert.NotEqual(nest.Parent.Second.Foo, n3.Parent.Second.Foo);
        }
    }
}