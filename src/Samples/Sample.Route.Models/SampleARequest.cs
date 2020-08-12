using System;
using Twino.Client.TMQ.Annotations;

namespace Sample.Route.Models
{
	[RouterName("sample-a-router")]
	[ContentType(31)]
	public class SampleARequest
	{
		public string Name { get; set; }

		public Guid Guid { get; set; }
	}
}