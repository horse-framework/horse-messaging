using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace HostedServiceSample.Producer
{
	[RouterName("test-router")]
	[DirectContentType(1)]
	internal class TestDirectModel
	{
		public string Foo { get; set; }
		public string Bar { get; set; }
	}
}