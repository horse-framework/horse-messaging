using System;
using System.IO;
using AdvancedSample.OrderService.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.OrderService.Context
{
	public class OrderContext : DbContext
	{
		public DbSet<OutboxMessage> OutboxMessages { get; set; }
		public DbSet<Order> Orders { get; set; }
		public DbSet<OrderSnapshot> OrderSnapshots { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"AdvancedSample\orders.db");
			optionsBuilder.UseSqlite($@"Data Source={path}");
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<OutboxMessage>();
		}
	}
}