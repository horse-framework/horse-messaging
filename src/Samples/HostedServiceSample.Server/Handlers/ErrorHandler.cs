using System;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Logging;
using Microsoft.Extensions.Logging;

namespace HostedServiceSample.Server.Handlers;

internal class ErrorHandler : IErrorHandler
{
    private readonly ILogger<ErrorHandler> _logger;

    public ErrorHandler(ILogger<ErrorHandler> logger)
    {
        _logger = logger;
    }

    public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
    {
        _logger.LogCritical(exception, "[EXCEPTION] {Message}", exception.Message);
    }
}