using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Scheduling;

/// <summary>
/// Scheduled task definition.
/// </summary>
public class ScheduledTask
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Unique name of the task.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Category of the task.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Cron expression for the task schedule.
    /// </summary>
    public string Schedule { get; set; }

    /// <summary>
    /// True if the task is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Total execution count of the task.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Total skip count of the task.
    /// </summary>
    public long SkipCount { get; set; }
    
    /// <summary>
    /// Last execution time of the task.
    /// </summary>
    public DateTime? LastExecution { get; set; }

    /// <summary>
    /// Result of the last execution.
    /// </summary>
    public bool? LastExecutionResult { get; set; }

    /// <summary>
    /// Next execution time of the task.
    /// </summary>
    public DateTime NextExecution { get; set; }

    /// <summary>
    /// Type of the task.
    /// </summary>
    public ScheduleType Type { get; set; }

    /// <summary>
    /// Target of the task (URL, Plugin Name, Queue Name or Router Name).
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Parameters of the task.
    /// </summary>
    public string Parameters { get; set; }

    /// <summary>
    /// Retry count if the task execution fails.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Skip queue name to push the error message if the task fails after all retries.
    /// </summary>
    public string SkipQueue { get; set; }

    /// <summary>
    /// Executes the task.
    /// </summary>
    public Task Execute(HorseRider rider)
    {
        switch (Type)
        {
            case ScheduleType.HttpRequest:
                return ExecuteHttpRequest(rider);

            case ScheduleType.PluginExecute:
                return ExecutePlugin(rider);

            case ScheduleType.QueuePush:
                return ExecuteQueuePush(rider);

            case ScheduleType.RouterPublish:
                return ExecuteRouterPublish(rider);

            default:
                return Task.CompletedTask;
        }
    }

    private async Task ExecuteHttpRequest(HorseRider rider)
    {
        if (string.IsNullOrEmpty(Target))
            return;

        string methodStr;
        string url;

        int firstSpace = Target.IndexOf(' ');
        if (firstSpace > 0)
        {
            methodStr = Target[..firstSpace].ToUpper();
            url = Target[(firstSpace + 1)..].Trim();
        }
        else
        {
            methodStr = "GET";
            url = Target.Trim();
        }

        HttpMethod method = new HttpMethod(methodStr);
        bool hasParameters = !string.IsNullOrEmpty(Parameters);

        if (method == HttpMethod.Get && hasParameters)
        {
            if (url.Contains('?'))
                url += "&" + Parameters;
            else
                url += "?" + Parameters;
        }

        using HttpRequestMessage request = new HttpRequestMessage(method, url);

        if (method != HttpMethod.Get && hasParameters)
        {
            string contentType = (Parameters.StartsWith('{') || Parameters.StartsWith('['))
                ? "application/json"
                : "application/x-www-form-urlencoded";

            request.Content = new StringContent(Parameters, Encoding.UTF8, contentType);
        }

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task ExecutePlugin(HorseRider rider)
    {
        HorsePlugin plugin = rider.Plugin.Plugins.FirstOrDefault(x => x.Name.Equals(Target, StringComparison.OrdinalIgnoreCase));
        if (plugin == null)
            throw new InvalidOperationException($"Plugin not found: {Target}");

        IHorsePluginHandler handler = plugin.GetDefaultRequestHandler();
        if (handler == null)
            throw new InvalidOperationException($"Default request handler not found for plugin: {Target}");

        HorseMessage message = new HorseMessage(MessageType.Plugin, Target);
        if (!string.IsNullOrEmpty(Parameters))
            message.SetStringContent(Parameters);

        HorsePluginContext context = new HorsePluginContext(HorsePluginEvent.PluginMessage, plugin, rider.Plugin, message);
        await handler.Execute(context);
    }

    private async Task ExecuteQueuePush(HorseRider rider)
    {
        HorseQueue queue = rider.Queue.Find(Target);
        if (queue == null)
            queue = await rider.Queue.Create(Target);

        if (queue == null)
            throw new InvalidOperationException($"Queue not found or could not be created: {Target}");

        HorseMessage message = new HorseMessage(MessageType.QueueMessage, Target);
        if (!string.IsNullOrEmpty(Parameters))
            message.SetStringContent(Parameters);

        await queue.Push(message);
    }

    private async Task ExecuteRouterPublish(HorseRider rider)
    {
        Router router = rider.Router.Find(Target);
        if (router == null)
            throw new InvalidOperationException($"Router not found: {Target}");

        HorseMessage message = new HorseMessage(MessageType.Router, Target);
        if (!string.IsNullOrEmpty(Parameters))
            message.SetStringContent(Parameters);

        await router.Publish(null, message);
    }
}