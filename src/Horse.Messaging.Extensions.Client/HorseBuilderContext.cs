using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Hosting application context
/// </summary>
public class HorseBuilderContext
{
    /// <summary>
    /// Service collection
    /// </summary>
    public IServiceCollection Services { get; set; }
    
    /// <summary>
    /// Hosting application configuration
    /// </summary>
    public IConfiguration Configuration { get; set; }
    
    /// <summary>
    /// Hosting environment
    /// </summary>
    public IHostEnvironment Environment { get; set; }
}