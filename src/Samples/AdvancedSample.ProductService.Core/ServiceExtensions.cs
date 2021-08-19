using AdvancedSample.DataAccess.Repository;
using AdvancedSample.ProductService.Context;
using AdvancedSample.ProductService.Core.BusinessManagers;
using AdvancedSample.ProductService.Core.BusinessManagers.Interfaces;
using AdvancedSample.ProductService.Core.Mappers;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.ProductService.Core
{
	public static class ServiceExtensions
	{
		public static void AddCoreServices(this IServiceCollection services)
		{
			services.AddMappers();
			services.AddUnitOfWork();
			services.AddDbContext<DbContext, ProductContext>(ServiceLifetime.Transient);
		}


		public static void AddBusinessManagers(this IServiceCollection services)
		{
			services.AddTransient<IProductBusinessManager, ProductBusinessManager>();
		}

		private static void AddMappers(this IServiceCollection services)
		{
			MapperConfiguration mapperConfiguration = new(mc => { mc.AddMapperProfiles(); });
			IMapper mapper = mapperConfiguration.CreateMapper();
			services.AddSingleton(mapper);
		}

		private static void AddMapperProfiles(this IMapperConfigurationExpression cfg)
		{
			cfg.AddProfile<ProductMapperProfile>();
		}
	}
}