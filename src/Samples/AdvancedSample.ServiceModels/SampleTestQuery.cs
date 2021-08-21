using System;
using AdvancedSample.Messaging.Common;
using AdvancedSample.Messaging.Common.ContentTypes.Queries;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.ServiceModels
{
	[RouterName(AdvancedSampleServiceRoutes.TestService)]
	[DirectContentType(QueryContentTypes.Test.SAMPLE_TEST_QUERY)]
	public class SampleTestQuery
	{
		public string Foo { get; set; }
	}

	public class SampleTestQueryResult
	{
		public string Bar { get; set; }
	}
}