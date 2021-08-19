using System;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdvancedSample.DataAccess.Repository
{
	public interface IUnitOfWork : IDisposable
	{
		public IQueryRepository<T> Query<T>() where T : class, IEntity;
		public ICommandRepository<T> Command<T>() where T : class, IEntity;
		public Task<int> SaveChangesAsync();
		public Task<IDbContextTransaction> BeginTransaction();
	}
}