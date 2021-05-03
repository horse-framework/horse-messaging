using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public abstract class ServiceQuery : IServiceQuery
	{
		public Guid QueryId { get; set; }
	}
}