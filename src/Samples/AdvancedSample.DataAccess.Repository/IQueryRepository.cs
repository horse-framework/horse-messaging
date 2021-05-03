using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;

namespace AdvancedSample.DataAccess.Repository
{
	public interface IQueryRepository<T> where T : IEntity
	{
		public Task<List<T>> GetAll();
		public Task<List<T>> FindAll(Expression<Func<T, bool>> predicate);
		public ValueTask<T> Get(int id);
		public Task<T> Find(Expression<Func<T, bool>> predicate);
	}
}