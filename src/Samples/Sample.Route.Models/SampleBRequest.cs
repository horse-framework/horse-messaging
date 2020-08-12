using System;
using Twino.Client.TMQ.Annotations;

namespace Sample.Route.Models
{
	[RouterName("sample-b-router")]
	[ContentType(32)]
	public class SampleBRequest
	{
		public string Name { get; set; }

		public Guid Guid { get; set; }
	}
}