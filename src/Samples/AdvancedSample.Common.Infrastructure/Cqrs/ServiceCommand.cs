using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public abstract class ServiceCommand : IServiceCommand
	{
		public Guid CommandId { get; set; }
	}
}