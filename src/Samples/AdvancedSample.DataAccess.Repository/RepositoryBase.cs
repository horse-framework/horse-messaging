using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.DataAccess.Repository
{
	internal abstract class RepositoryBase<T> : ICommandRepository<T>, IQueryRepository<T>
		where T : class, IEntity
	{
		private readonly DbSet<T> _table;

		protected RepositoryBase(DbContext context) => _table = context.Set<T>();

		public ValueTask<EntityEntry<T>> AddAsync(T entity)
		{
			entity.CreatedAt = DateTime.Now;
			return _table.AddAsync(entity);
		}

		public EntityEntry<T> Update(T entity)
		{
			entity.UpdatedAt = DateTime.Now;
			return _table.Update(entity);
		}

		public EntityEntry<T> Delete(T entity)
		{
			entity.IsDeleted = true;
			entity.DeletedAt = DateTime.Now;
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

		public EntityEntry<T> DeletePermanently(T entity) => _table.Remove(entity);
		public Task<List<T>> GetAll() => _table.ToListAsync();
		public Task<List<T>> FindAll(Expression<Func<T, bool>> predicate) => _table.Where(predicate).ToListAsync();
		public ValueTask<T> Get(int id) => _table.FindAsync(id);
		public Task<T> Find(Expression<Func<T, bool>> predicate) => _table.FirstAsync(predicate);
	}
}