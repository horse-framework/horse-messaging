using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdvancedSample.Core
{
	internal static class Configure
	{
		public static void ConfigureHost(this IConfigurationBuilder builder)
		{
			// Default host builder'da prefix DOTNET, web hostta ise ASPNETCORE olarak geliyor. 
			// Tüm application'larda prefix'in sabit olması için eziyoruz.
			builder.AddEnvironmentVariables("SAMPLE_");
		}

		public static void ConfigureApp(this IConfigurationBuilder builder, HostBuilderContext hostContext)
		{
			// Default host builder'da appsettings.json dosyaları optional.
			// Bizim kurguladığımız yapıda zorunlu. Bu nedenle eziyoruz.
			IHostEnvironment env = hostContext.HostingEnvironment;
			builder
			   .SetBasePath(Directory.GetCurrentDirectory())
			   .AddJsonFile("appsettings.json", false, true)
			   .AddJsonFile($"appsettings.{env.EnvironmentName}.json", false, true);
		}

		public static void ConfigureLogging(this ILoggingBuilder builder, HostBuilderContext hostContext)
		{
			// Default host builder ile çalıştığımız için bu metodu kullanmaya gerek yok.
			// Custom builder ile çalışmak istersek log için configure etmemiz gerek. Önlem amaçlı yazıldı.
			builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
			builder.AddConsole();
			builder.AddDebug();
			builder.AddEventSourceLogger();
		}
	}
}