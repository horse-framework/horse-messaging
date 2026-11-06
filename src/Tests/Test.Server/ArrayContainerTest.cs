using Horse.Messaging.Server.Containers;
using Xunit;

namespace Test.Server;

public class ArrayContainerTest
{
    [Fact]
    public void RemoveAll_DoesNotMutateSnapshotHeldByReaders()
    {
        ArrayContainer<string> container = new ArrayContainer<string>();
        container.Add("a");
        container.Add("b");
        container.Add("c");

        // A reader (e.g. an event trigger loop) holds the array returned by All().
        string[] snapshot = container.All();

        container.RemoveAll(x => x == "b");

        Assert.Equal(new[] {"a", "b", "c"}, snapshot);
        Assert.Equal(new[] {"a", "c"}, container.All());
    }

    [Fact]
    public void RemoveAll_NoMatch_KeepsSameArray()
    {
        ArrayContainer<string> container = new ArrayContainer<string>();
        container.Add("a");
        string[] before = container.All();

        container.RemoveAll(x => x == "z");

        Assert.Same(before, container.All());
    }
}
