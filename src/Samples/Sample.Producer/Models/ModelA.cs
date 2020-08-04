using Twino.Client.TMQ.Annotations;

namespace Sample.Producer.Models
{
	[RouterName("deneme-router")]
	[ContentType(1001)]
	public class ModelA
	{
		public string Foo { get; set; }
	}
}