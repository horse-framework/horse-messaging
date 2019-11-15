using System;
using Microsoft.EntityFrameworkCore;
using Twino.Ioc;
using Twino.Ioc.Pool;

namespace Twino.Extensions.Data
{
    public static class DataExtensions
    {
        #region Add Context

        /// <summary>
        /// Adds database context as scoped
        /// </summary>
        public static IServiceContainer AddDataContextScoped<TContext>(this IServiceContainer services,
                                                                       Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddScoped<TContext, TContext>();

            return services;
        }

        /// <summary>
        /// Adds database context as transient
        /// </summary>
        public static IServiceContainer AddDataContextTransient<TContext>(this IServiceContainer services,
                                                                          Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddTransient<TContext, TContext>();
            
            return services;
        }

        #endregion

        #region Add Scoped Pool

        /// <summary>
        /// Adds database context as scoped pool. Pool options are default.
        /// </summary>
        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);
            
            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>();

            return services;
        }

        /// <summary>
        /// Adds database context as scoped pool.
        /// Pool options will be decided in method specified in 3rd parameter.
        /// </summary>
        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                           Action<ServicePoolOptions> poolOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>(poolOptions);

            return services;
        }

        /// <summary>
        /// Adds database context as scoped pool.
        /// Pool options will be decided in method specified in 3rd parameter.
        /// In last parameter will be called after each instance is created.
        /// </summary>
        public static IServiceContainer AddDataContextScopedPool<TContext>(this IServiceContainer services,
                                                                           Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                           Action<ServicePoolOptions> poolOptions,
                                                                           Action<TContext> afterInstanceCreated)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddScopedPool<TContext, TContext>(poolOptions, afterInstanceCreated);

            return services;
        }

        #endregion

        #region Add Transient Pool

        /// <summary>
        /// Adds database context as transient pool. Pool options are default.
        /// </summary>
        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>();

            return services;
        }

        /// <summary>
        /// Adds database context as transient pool.
        /// Pool options will be decided in method specified in 3rd parameter.
        /// </summary>
        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                              Action<ServicePoolOptions> poolOptions)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>(poolOptions);

            return services;
        }

        /// <summary>
        /// Adds database context as transient pool.
        /// Pool options will be decided in method specified in 3rd parameter.
        /// In last parameter will be called after each instance is created.
        /// </summary>
        public static IServiceContainer AddDataContextTransientPool<TContext>(this IServiceContainer services,
                                                                              Action<DbContextOptionsBuilder<TContext>> contextOptions,
                                                                              Action<ServicePoolOptions> poolOptions,
                                                                              Action<TContext> afterInstanceCreated)
            where TContext : DbContext
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>();
            contextOptions(builder);

            services.AddSingleton(builder.Options);
            services.AddTransientPool<TContext, TContext>(poolOptions, afterInstanceCreated);

            return services;
        }

        #endregion
    }
}