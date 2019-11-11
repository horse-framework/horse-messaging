using System;
using Microsoft.EntityFrameworkCore;
using Twino.Ioc;
using Twino.Ioc.Pool;

namespace Twino.Mvc.Data
{
    public static class AddExtensions
    {
        #region Add Context

        public static IServiceContainer AddDataContextScoped<TContext>(this IServiceContainer services,
                                                                       Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddScoped<TContext, TContext>();
            
            return services;
        }

        public static IServiceContainer AddDataContextTransient<TContext>(this IServiceContainer services,
                                                                          Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddTransient<TContext, TContext>();
            
            return services;
        }

        #endregion

        #region Add Scoped Pool

        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>();
            
            return services;
        }

        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                           Action<ServicePoolOptions> poolOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>(poolOptions);
            
            return services;
        }

        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                           Action<ServicePoolOptions> poolOptions,
                                                                           Action<TContext> afterInstanceCreated)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>(poolOptions, afterInstanceCreated);
            
            return services;
        }

        #endregion

        #region Add Transient Pool

        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>();
            
            return services;
        }

        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                              Action<ServicePoolOptions> poolOptions)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>(poolOptions);
            
            return services;
        }

        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                              Action<ServicePoolOptions> poolOptions,
                                                                              Action<TContext> afterInstanceCreated)
            where TContext : DbContext
        {
            DbContextOptions<TContext> options = new DbContextOptions<TContext>();
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>(options);
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>(poolOptions, afterInstanceCreated);
            
            return services;
        }

        #endregion
    }
}