namespace Test.Ioc.Services
{
    public class NestParentService : INestParentService
    {
        public string Foo { get; set; }
        
        public IParentService Parent { get; }
        public ISingleService Single { get; }

        public NestParentService(IParentService parentService, ISingleService singleService)
        {
            Parent = parentService;
            Single = singleService;
        }
    }
}