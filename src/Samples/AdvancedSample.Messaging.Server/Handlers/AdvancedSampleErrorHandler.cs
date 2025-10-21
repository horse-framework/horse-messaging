using System;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;
using Microsoft.Extensions.Logging;

namespace AdvancedSample.Messaging.Server.Handlers;

internal class AdvancedSampleErrorHandler : IErrorHandler
{
    private readonly ILogger<AdvancedSampleErrorHandler> _logger;

    public AdvancedSampleErrorHandler(ILogger<AdvancedSampleErrorHandler> logger)
    {
        _logger = logger;
    }

    public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        _logger.LogCritical(exception, "[EXCEPTION] {Message}", exception.Message);
    }
}