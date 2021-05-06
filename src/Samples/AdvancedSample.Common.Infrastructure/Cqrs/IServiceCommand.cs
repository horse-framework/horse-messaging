using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public interface IServiceCommand : IServiceMessage
	{
		public Guid CommandId { get; }
	}
}