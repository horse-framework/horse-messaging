using System;
using System.Threading;
using System.Threading.Tasks;

namespace Horse.Messaging.Client;

internal sealed class GracefulShutdownOptions
{
    public TimeSpan MinWait { get; init; }
    public TimeSpan MaxWait { get; init; }
    public Func<Task> ShuttingDownAction { get; init; }
    public Func<IServiceProvider, Task> ShuttingDownActionWithProvider { get; init; }
}