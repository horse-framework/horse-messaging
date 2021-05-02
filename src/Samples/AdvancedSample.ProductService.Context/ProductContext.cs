using System;
using AdvancedSample.ProductService.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.ProductService.Context
{
	public class ProductContext : DbContext
	{
		public DbSet<Product> Products { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			optionsBuilder.UseSqlite("Data Source=:memory:");
		}
	}
}