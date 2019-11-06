using Test.Ioc.Services;
using Twino.Ioc;
using Xunit;

namespace Test.Ioc
{
    public class TransientTest
    {
        [Fact]
        public async void Single()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<ISingleService, SingleService>();

            ISingleService s1 = await services.Get<ISingleService>();
            s1.Foo = "a";

            //s1 and s2 should not be equal because they should be different instances
            ISingleService s2 = await services.Get<ISingleService>();
            Assert.NotEqual(s1.Foo, s2.Foo);

            //s1 or s2 should not equal to scoped transient instance
            IContainerScope scope = services.CreateScope();
            ISingleService s3 = await services.Get<ISingleService>(scope);
            Assert.NotEqual(s1.Foo, s3.Foo);
            s3.Foo = "b";

            //two transient instances in same scope should not be equal
            ISingleService s4 = await services.Get<ISingleService>(scope);
            Assert.NotEqual(s1.Foo, s4.Foo);
            Assert.NotEqual(s3.Foo, s4.Foo);
        }

        [Fact]
        public async void Nested()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            IParentService parent = await services.Get<IParentService>();
            parent.Foo = "parent";
            parent.First.Foo = "first";
            parent.Second.Foo = "second";

            IParentService p1 = await services.Get<IParentService>();
            Assert.NotEqual(parent.Foo, p1.Foo);
            Assert.NotEqual(parent.First, p1.First);
            Assert.NotEqual(parent.Second, p1.Second);
            Assert.NotEqual(parent.First.Foo, p1.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p1.Second.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>();
            ISecondChildService second = await services.Get<ISecondChildService>();
            Assert.NotEqual(parent.First, first);
            Assert.NotEqual(parent.First.Foo, first.Foo);
            Assert.NotEqual(parent.Second.Foo, second.Foo);

            //scoped
            IContainerScope scope = services.CreateScope();
            IParentService p2 = await services.Get<IParentService>(scope);
            Assert.NotEqual(parent.Foo, p2.Foo);
            Assert.NotEqual(parent.First, p2.First);
            Assert.NotEqual(parent.Second, p2.Second);
            Assert.NotEqual(parent.First.Foo, p2.First.Foo);
            Assert.NotEqual(parent.Second.Foo, p2.Second.Foo);

            IFirstChildService firstScoped = await services.Get<IFirstChildService>(scope);
            ISecondChildService secondScoped = await services.Get<ISecondChildService>(scope);
            Assert.NotEqual(parent.First, firstScoped);
            Assert.NotEqual(parent.First.Foo, firstScoped.Foo);
            Assert.NotEqual(parent.Second.Foo, secondScoped.Foo);
        }

        [Fact]
        public async void MultipleNestedDoubleParameter()
        {
            ServiceContainer services = new ServiceContainer();
            services.AddTransient<INestParentService, NestParentService>();
            services.AddTransient<ISingleService, SingleService>();
            services.AddTransient<IParentService, ParentService>();
            services.AddTransient<IFirstChildService, FirstChildService>();
            services.AddTransient<ISecondChildService, SecondChildService>();

            INestParentService nest = await services.Get<INestParentService>();
            nest.Foo = "nest";
            nest.Parent.Foo = "parent";
            nest.Parent.First.Foo = "first";
            nest.Parent.Second.Foo = "second";
            nest.Single.Foo = "single";

            INestParentService n1 = await services.Get<INestParentService>();
            Assert.NotEqual(nest.Foo, n1.Foo);
            Assert.NotEqual(nest.Single.Foo, n1.Single.Foo);
            Assert.NotEqual(nest.Parent.Foo, n1.Parent.Foo);
            Assert.NotEqual(nest.Parent.First.Foo, n1.Parent.First.Foo);
            Assert.NotEqual(nest.Parent.Second.Foo, n1.Parent.Second.Foo);

            IParentService parent = await services.Get<IParentService>();
            Assert.NotEqual(nest.Parent.Foo, parent.Foo);
            Assert.NotEqual(nest.Parent.First.Foo, parent.First.Foo);
            Assert.NotEqual(nest.Parent.Second.Foo, parent.Second.Foo);

            ISingleService single = await services.Get<ISingleService>();
            Assert.NotEqual(nest.Single.Foo, single.Foo);

            IFirstChildService first = await services.Get<IFirstChildService>();
            Assert.NotEqual(nest.Parent.First.Foo, first.Foo);
            
            ISecondChildService second = await services.Get<ISecondChildService>();
            Assert.NotEqual(nest.Parent.Second.Foo, second.Foo);

            IContainerScope scope = services.CreateScope();

            INestParentService n2 = await services.Get<INestParentService>(scope);
            Assert.NotEqual(nest.Foo, n2.Foo);
            Assert.NotEqual(nest.Single.Foo, n2.Single.Foo);
            Assert.NotEqual(nest.Parent.Foo, n2.Parent.Foo);
            Assert.NotEqual(nest.Parent.First.Foo, n2.Parent.First.Foo);
            Assert.NotEqual(nest.Parent.Second.Foo, n2.Parent.Second.Foo);
            n2.Foo = "nest-2";
            n2.Parent.Foo = "parent-2";
            n2.Parent.First.Foo = "first-2";
            n2.Parent.Second.Foo = "second-2";
            n2.Single.Foo = "single-2";
            
            INestParentService n3 = await services.Get<INestParentService>(scope);
            Assert.NotEqual(n2.Foo, n3.Foo);
            Assert.NotEqual(n2.Single.Foo, n3.Single.Foo);
            Assert.NotEqual(n2.Parent.Foo, n3.Parent.Foo);
            Assert.NotEqual(n2.Parent.First.Foo, n3.Parent.First.Foo);
            Assert.NotEqual(n2.Parent.Second.Foo, n3.Parent.Second.Foo);
        }
    }
}