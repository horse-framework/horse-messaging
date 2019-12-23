namespace Test.Ioc.Services
{
    public class ParentService : IParentService
    {
        public string Foo { get; set; }

        public IFirstChildService First { get; }
        public ISecondChildService Second { get; }

        public ParentService(IFirstChildService firstChildService, ISecondChildService secondChildService)
        {
            First = firstChildService;
            Second = secondChildService;
        }
    }
}