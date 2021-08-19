using System;
using System.IO;
using AdvancedSample.ProductService.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.ProductService.Context
{
	public class ProductContext : DbContext
	{
		public DbSet<Product> Products { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"AdvancedSample\products.db");
			optionsBuilder.UseSqlite($@"Data Source={path}");
		}
	}
}