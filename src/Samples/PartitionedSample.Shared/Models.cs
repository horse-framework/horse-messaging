using System.Threading;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

public static class Test
{
    public static bool MessageConsumed;
    public static bool Message2Consumed;
    public static bool Message3Consumed;
    public static bool Message4Consumed;
}

public class TestEvent
{
    public string Foo { get; set; } = "Foo";
}

public class Test2Event
{
    public string Foo { get; set; } = "Foo2";
}

[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
public class Test3Event
{
    public string Foo { get; set; } = "Foo3";
}

public class Test4Event
{
    public string Foo { get; set; } = "Foo4";
}