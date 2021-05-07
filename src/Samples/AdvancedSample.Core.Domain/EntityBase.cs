using System;

namespace AdvancedSample.Core.Domain
{
	public abstract class EntityBase : IEntity
	{
		public int Id { get; set; }
	}

	public abstract class SoftDeletableEntity : EntityBase, ISoftDeletableEntity
	{
		public bool IsDeleted { get; set; }
		public DateTime DeletedAt { get; set; }
	}

	public abstract class AuditableEntity : EntityBase, IAuditableEntity
	{
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}

	public abstract class Entity : AuditableEntity, ISoftDeletableEntity
	{
		public bool IsDeleted { get; set; }
		public DateTime DeletedAt { get; set; }
	}
}