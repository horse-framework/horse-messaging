using AdvancedSample.Messaging.Common;
using AdvancedSample.Messaging.Common.ContentTypes;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.ServiceModels
{
	[RouterName(AdvancedSampleServiceRoutes.TestService)]
	[DirectContentType(ServiceContentTypes.SAMPLE_TEST_QUERY)]
	public class SampleTestQuery
	{
		public string Foo { get; set; }
	}

	public class SampleTestQueryResult
	{
		public string Bar { get; set; }
	}


	[RouterName(AdvancedSampleServiceRoutes.TestService)]
	[DirectContentType(ServiceContentTypes.SAMPLE_TEST_COMMAND)]
	public class SampleTestCommand
	{
		public string Foo { get; set; }
	}

	public class SampleTestEvent
	{
		public string Foo { get; set; }
	}
}