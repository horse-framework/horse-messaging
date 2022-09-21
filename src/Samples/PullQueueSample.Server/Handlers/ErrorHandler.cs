using Horse.Messaging.Server;
using Microsoft.Extensions.Logging;

namespace PullQueueSample.Server.Handlers;

internal class ErrorHandler : IErrorHandler
{
    private readonly ILogger<ErrorHandler> _logger;

    public ErrorHandler(ILogger<ErrorHandler> logger)
    {
        _logger = logger;
    }

    public void Error(string hint, Exception exception, string payload)
    {
        _logger.LogCritical(exception, "[EXCEPTION] {Message}", exception.Message);
    }
}