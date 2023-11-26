using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace AdvancedSample.Core;

public static class SampleConfig
{
    public static IConfigurationRoot Configure(string[] args)
    {
        string environment = Environment.GetEnvironmentVariable("SAMPLE_ENVIRONMENT");
#if DEBUG
        if (string.IsNullOrEmpty(environment))
        {
            Environment.SetEnvironmentVariable("SAMPLE_ENVIRONMENT", "Development");
            environment = "Development";
        }
#endif

        var builder = new ConfigurationBuilder();
        return builder.ConfigureHost()
            .ConfigureApp(environment)
            .Build();
    }

    public static IConfigurationBuilder ConfigureHost(this IConfigurationBuilder builder)
    {
        // Default host builder'da prefix DOTNET, web hostta ise ASPNETCORE olarak geliyor. 
        // Tüm application'larda prefix'in sabit olması için eziyoruz.
        return builder.AddEnvironmentVariables("SAMPLE_");
    }

    public static IConfigurationBuilder ConfigureApp(this IConfigurationBuilder builder, HostBuilderContext hostContext)
    {
        return builder.ConfigureApp(hostContext.HostingEnvironment.EnvironmentName);
    }

    private static IConfigurationBuilder ConfigureApp(this IConfigurationBuilder builder, string environment)
    {
        // Default host builder'da appsettings.json dosyaları optional.
        // Bizim kurguladığımız yapıda zorunlu. Bu nedenle eziyoruz.
        return builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings-base.json", true)
            .AddJsonFile($"appsettings-base.{environment}.json", true)
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{environment}.json", true, true);
    }

    public static void ConfigureLogging(this ILoggingBuilder builder, HostBuilderContext hostContext)
    {
        builder.ConfigureLogging(hostContext.Configuration);
    }

    public static void ConfigureLogging(this ILoggingBuilder builder, IConfiguration configuration)
    {
        // Default host builder ile çalıştığımız için bu metodu kullanmaya gerek yok.
        // Custom builder ile çalışmak istersek log için configure etmemiz gerek. Önlem amaçlı yazıldı.
        if (configuration is not null)
            builder.AddConfiguration(configuration.GetSection("Logging"));
        builder.AddConsole();
        builder.AddDebug();
        builder.AddEventSourceLogger();
    }
}