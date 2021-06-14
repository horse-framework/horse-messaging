using AdvancedSample.Core;
using Microsoft.Extensions.Configuration;

namespace AdvancedSample.Service
{
	internal class SampleServiceProviderFactory<T> : ServiceProviderFactory<T>
		where T : class
	{
		public SampleServiceProviderFactory(string clientType, IConfiguration configuration) : base(clientType, configuration) { }
	}
}