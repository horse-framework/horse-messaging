using System;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace RoutingSample.Models
{
	[RouterName("GIVE-ME-REQUEST-ROUTER")]
	[DirectContentType(1002)]
	public class GiveMeGuidRequest
	{
		public string Foo { get; set; }
	}

	public class GiveMeGuidResponse
	{
		public Guid Guid { get; set; }
	}
}