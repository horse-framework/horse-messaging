using System;
using Horse.Messaging.Client;
using Microsoft.Extensions.Hosting;

namespace Horse.Messaging.Extensions.Client
{
	/// <summary>
	/// Horse messaging client extensions
	/// </summary>
	public static class GenericHostExtensions
	{
		/// <summary>
		/// Configure your horse client.
		/// </summary>
		/// <param name="hostBuilder">IHostBuilder</param>
		/// <param name="configureDelegate">Configure delegate</param>
		public static IHostBuilder ConfigureHorseClient(this IHostBuilder hostBuilder, Action<HostBuilderContext, HorseClientBuilder> configureDelegate)
		{
			hostBuilder.Properties.Add("HasHorseClientBuilderDelegateContext", null);
			return hostBuilder.ConfigureHorseClientInternal(configureDelegate);
		}

		/// <summary>
		/// Configure your horse client.
		/// </summary>
		/// <param name="hostBuilder">IHostBuilder</param>
		/// <param name="configureDelegate">Configure delegate</param>
		public static IHostBuilder ConfigureHorseClient(this IHostBuilder hostBuilder, Action<HorseClientBuilder> configureDelegate)
		{
			return hostBuilder.ConfigureHorseClientInternal(configureDelegate);
		}

		private static IHostBuilder ConfigureHorseClientInternal(this IHostBuilder hostBuilder, object configureDelegate)
		{
			const string _clientBuilderDelegate = "HorseClientBuilderDelegate";
			if (hostBuilder.Properties.ContainsKey(_clientBuilderDelegate))
				throw new InvalidOperationException("Horse client was already configured.");
			hostBuilder.Properties.Add(_clientBuilderDelegate, configureDelegate);
			return hostBuilder;
		}
	}
}