using System;
using Horse.Messaging.Server;
using Microsoft.Extensions.Logging;

namespace AdvancedSample.Messaging.Server.Handlers
{
	internal class AdvancedSampleErrorHandler : IErrorHandler
	{
		private readonly ILogger<AdvancedSampleErrorHandler> _logger;

		public AdvancedSampleErrorHandler(ILogger<AdvancedSampleErrorHandler> logger)
		{
			_logger = logger;
		}

		public void Error(string hint, Exception exception, string payload)
		{
			_logger.LogCritical(exception, "[EXCEPTION] {Message}", exception.Message);
		}
	}
}