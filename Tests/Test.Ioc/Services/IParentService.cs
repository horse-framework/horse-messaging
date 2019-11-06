namespace Test.Ioc.Services
{
    public interface IParentService
    {
        string Foo { get; set; }

        IFirstChildService First { get; }
        ISecondChildService Second { get; }
    }
}