using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Helpers;

/// <summary>
/// Reports failures of user event handlers that are called without waiting.
/// A handler that throws synchronously must not break the caller (on the receive path it disconnects the client),
/// and a handler task that fails later must not be lost.
/// </summary>
internal static class HandlerFailureObserver
{
    /// <summary>
    /// Reports the handler task if it fails, without waiting for it.
    /// Allocates nothing when the task has already completed successfully.
    /// </summary>
    internal static void Observe(HorseRider rider, Task task, int eventId, string handlerName)
    {
        if (task == null || task.IsCompletedSuccessfully)
            return;

        _ = ReportWhenFailed(rider, task, eventId, handlerName);
    }

    /// <summary>
    /// Reports a synchronous handler exception
    /// </summary>
    internal static void Report(HorseRider rider, Exception exception, int eventId, string handlerName)
    {
        rider.SendError(HorseLogLevel.Error, eventId, $"{handlerName} failed", exception);
    }

    private static async Task ReportWhenFailed(HorseRider rider, Task task, int eventId, string handlerName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Report(rider, e, eventId, handlerName);
        }
    }
}
