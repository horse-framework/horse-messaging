using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public abstract class ServiceQuery : IServiceQuery
	{
		public Guid QueryId { get; }

		protected ServiceQuery()
		{
			QueryId = Guid.NewGuid();
		}
	}
}