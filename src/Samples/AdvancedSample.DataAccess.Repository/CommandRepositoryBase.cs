using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.DataAccess.Repository
{
	internal abstract class CommandRepositoryBase<T> : ICommandRepository<T>
		where T : class, IEntity
	{
		private readonly DbSet<T> _table;

		protected CommandRepositoryBase(DbContext context)
		{
			_table = context.Set<T>();
		}

		public ValueTask<EntityEntry<T>> AddAsync(T entity)
		{
			if (entity is IAuditableEntity auditableEntity)
				auditableEntity.CreatedAt = DateTime.Now;
			return _table.AddAsync(entity);
		}

		public EntityEntry<T> Update(T entity)
		{
			if (entity is IAuditableEntity auditableEntity)
				auditableEntity.CreatedAt = DateTime.Now;
			return _table.Update(entity);
		}

		public EntityEntry<T> Delete(T entity)
		{
			if (entity is not ISoftDeletableEntity softDeletableEntity)
				return _table.Update(entity);
			softDeletableEntity.IsDeleted = true;
			softDeletableEntity.DeletedAt = DateTime.Now;

			return _table.Update(entity);
		}

		public async Task<EntityEntry<T>> Delete(int id)
		{
			T entity = await _table.FindAsync(id);
			return Delete(entity);
		}

		public async Task<EntityEntry<T>> DeletePermanently(int id)
		{
			T entity = await _table.FindAsync(id);
			return DeletePermanently(entity);
		}

		public EntityEntry<T> DeletePermanently(T entity)
		{
			return _table.Remove(entity);
		}
	}
}