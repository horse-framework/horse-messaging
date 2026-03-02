using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace infra.messaging;

public sealed partial class HorseClientService : IHostedService
{
    private readonly HorseClient _horseClient;
    private readonly ILogger<HorseClientService> _logger;

    public HorseClientService(ILogger<HorseClientService> logger, HorseClient horseClient, IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _horseClient = horseClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _horseClient.Connected += Connected;
        _horseClient.Disconnected += Disconnected;
        _horseClient.MessageReceived += MessageReceived;
        _horseClient.Error += ExceptionThrown;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _horseClient.Connected -= Connected;
        _horseClient.Disconnected -= Disconnected;
        _horseClient.MessageReceived -= MessageReceived;
        _horseClient.Error -= ExceptionThrown;
        _horseClient.Disconnect();
        _horseClient.Dispose();
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        return Task.CompletedTask;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogUnhandledException(_logger, (Exception)e.ExceptionObject);
        if (e.IsTerminating) LogApplicationTerminating(_logger);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogUnobservedTaskException(_logger, e.Exception);
        e.SetObserved();
    }

    private void Connected(HorseClient client)
    {
        LogClientConnected(_logger);
    }

    private void Disconnected(HorseClient client)
    {
        LogClientDisconnected(_logger);
    }

    private void MessageReceived(HorseClient client, HorseMessage message)
    {
        LogMessagingReceived(message.MessageId);
    }

    private void ExceptionThrown(HorseClient client, Exception ex, HorseMessage message)
    {
        LogClientException(_logger, client.ClientId, ex);
    }

    [LoggerMessage(EventId = 2030, Level = LogLevel.Critical, Message = "[MESSAGING][UNHANDLED_EXCEPTION]")]
    static partial void LogUnhandledException(ILogger<HorseClientService> logger, Exception exception);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Critical, Message = "[MESSAGING][APPLICATION_TERMINATING]")]
    static partial void LogApplicationTerminating(ILogger<HorseClientService> logger);

    [LoggerMessage(EventId = 2032, Level = LogLevel.Critical, Message = "[MESSAGING][UNOBSERVED_TASK_EXCEPTION]")]
    static partial void LogUnobservedTaskException(ILogger<HorseClientService> logger, Exception exception);

    [LoggerMessage(EventId = 2033, Level = LogLevel.Information, Message = "[MESSAGING][CONNECTED] Server=HorseMQ")]
    static partial void LogClientConnected(ILogger<HorseClientService> logger);

    [LoggerMessage(EventId = 2034, Level = LogLevel.Information, Message = "[MESSAGING][DISCONNECTED] Server=HorseMQ")]
    static partial void LogClientDisconnected(ILogger<HorseClientService> logger);

    [LoggerMessage(EventId = 2035, Level = LogLevel.Error, Message = "[MESSAGING][CLIENT_ERROR] {clientId}")]
    static partial void LogClientException(ILogger<HorseClientService> logger, string clientId, Exception exception);

    [LoggerMessage(EventId = 2036, Level = LogLevel.Debug, Message = "[MESSAGING][RECEIVED] {messageMessageId}")]
    partial void LogMessagingReceived(string messageMessageId);
}