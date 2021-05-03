using System;
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
		private readonly DbContext _context;
		private readonly DbSet<T> _table;

		protected RepositoryBase(DbContext context)
		{
			_context = context;
			_table = context.Set<T>();
		}

		public async ValueTask<EntityEntry<T>> AddAsync(T entity)
		{
			entity.CreatedAt = DateTime.Now;
			EntityEntry<T> entry = await _table.AddAsync(entity);
			await _context.SaveChangesAsync();
			return entry;
		}

		public async Task<EntityEntry<T>> Update(T entity)
		{
			entity.UpdatedAt = DateTime.Now;
			EntityEntry<T> entry = _table.Update(entity);
			await _context.SaveChangesAsync();
			return entry;
		}

		public async Task<EntityEntry<T>> Delete(T entity)
		{
			entity.IsDeleted = true;
			entity.DeletedAt = DateTime.Now;
			EntityEntry<T> entry = _table.Update(entity);
			await _context.SaveChangesAsync();
			return entry;
		}

		public async Task<EntityEntry<T>> Delete(int id)
		{
			T entity = await _table.FindAsync(id);
			return await Delete(entity);
		}

		public async Task<EntityEntry<T>> DeletePermanently(int id)
		{
			T entity = await _table.FindAsync(id);
			return await DeletePermanently(entity);
		}

		public async Task<EntityEntry<T>> DeletePermanently(T entity)
		{
			EntityEntry<T> entry = _table.Remove(entity);
			await _context.SaveChangesAsync();
			return entry;
		}

		public IQueryable<T> GetAll()
		{
			return _table;
		}

		public IQueryable<T> FindAll(Expression<Func<T, bool>> predicate)
		{
			return _table.Where(predicate);
		}

		public ValueTask<T> Get(int id)
		{
			return _table.FindAsync(id);
		}

		public Task<T> Find(Expression<Func<T, bool>> predicate)
		{
			return _table.FirstAsync(predicate);
		}
	}
}