using Horse.Messaging.Client.Routers.Annotations;

namespace HostedServiceSample.Producer;

internal class TestQueueModel
{
    public string Foo { get; set; }
    public string Bar { get; set; }
}
	
[RouterName("test-queue-ro1uter")]
public class TestQueueModel2
{
    public string Foo { get; set; }
    public string Bar { get; set; }
}