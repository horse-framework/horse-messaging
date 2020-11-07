using System;
using Twino.MQ.Client.Annotations;

namespace RoutingSample.Models
{
	[RouterName("GIVE-ME-REQUEST-ROUTER")]
	[ContentType(1002)]
	public class GiveMeGuidRequest
	{
		public string Foo { get; set; }
	}

	public class GiveMeGuidResponse
	{
		public Guid Guid { get; set; }
	}
}