using System;
using System.IO;
using AdvancedSample.OutboxService.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.OutboxService.Context
{
	public class OutboxContext : DbContext
	{
		public DbSet<OutboxMessage> Messages { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"AdvancedSample\outbox.db");
			optionsBuilder.UseSqlite($@"Data Source={path}");
		}
	}
}