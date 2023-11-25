using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HostedServiceSample.Common;

public static class Config
{
    public static IConfigurationRoot Configure(string[] args)
    {
        string environment = Environment.GetEnvironmentVariable("HORSE_ENVIRONMENT");

#if DEBUG
        if (string.IsNullOrEmpty(environment))
        {
            Environment.SetEnvironmentVariable("HORSE_ENVIRONMENT", "Development");
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
        return builder.AddEnvironmentVariables("HORSE_");
    }

    public static IConfigurationBuilder ConfigureApp(this IConfigurationBuilder builder, HostBuilderContext hostContext)
    {
        return builder.ConfigureApp(hostContext.HostingEnvironment.EnvironmentName);
    }

    private static IConfigurationBuilder ConfigureApp(this IConfigurationBuilder builder, string environment)
    {
        return builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{environment}.json", true, true);
    }

    public static void ConfigureLogging(this ILoggingBuilder builder, HostBuilderContext hostContext)
    {
        builder.ConfigureLogging(hostContext.Configuration);
    }

    public static void ConfigureLogging(this ILoggingBuilder builder, IConfiguration configuration)
    {
        if (configuration is not null)
            builder.AddConfiguration(configuration.GetSection("Logging"));
        builder.AddConsole();
        builder.AddDebug();
        builder.AddEventSourceLogger();
    }
}