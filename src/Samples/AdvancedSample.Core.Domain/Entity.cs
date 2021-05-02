using System;

namespace AdvancedSample.Core.Domain
{
	public abstract class Entity : IEntity
	{
		public int Id { get; set; }
		public bool IsDeleted { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public DateTime DeletedAt { get; set; }
	}
}