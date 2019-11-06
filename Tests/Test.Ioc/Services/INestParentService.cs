namespace Test.Ioc.Services
{
    public interface INestParentService
    {
        string Foo { get; set; }

        IParentService Parent { get; }
        ISingleService Single { get; }
    }
}