namespace Test.Common.Models;

public class QueueMessageA
{
    public string Foo { get; set; }

    public QueueMessageA(string foo)
    {
        Foo = foo;
    }
}