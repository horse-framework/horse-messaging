using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// HTTP Binding.
/// Targets Endpoints.
/// Message is send as Querystring for GET and DELETE methods.
/// For other methods sends as JSON body.
/// </summary>
public class HttpBinding : Binding
{
    /// <summary>
    /// Shared by every HTTP binding, so connections are pooled and reused.
    /// A client per message opened a new connection for each message and left sockets in TIME_WAIT.
    /// Pooled connections are renewed periodically, so DNS changes of the endpoints are picked up.
    /// </summary>
    private static readonly HttpClient Client = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    /// <summary>
    /// Maximum duration of an HTTP request sent by HTTP bindings. Default is 100 seconds.
    /// The router publish (and the producer's connection) waits for the request,
    /// so a shorter value limits how long a slow endpoint holds the producer.
    /// </summary>
    public static TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Sends message to target as HTTP request and waits for response.
    /// Ok Accepted and Created responses return true, others return false
    /// </summary>
    public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        try
        {
            // ReSharper disable once PossibleInvalidOperationException
            HttpBindingMethod method = (HttpBindingMethod) ContentType.Value;
            string content = message.Length > 0 ? message.ToString() : null;

            using CancellationTokenSource timeout = new CancellationTokenSource(RequestTimeout);
            CancellationToken token = timeout.Token;
            Task<HttpResponseMessage> response = null;

            switch (method)
            {
                case HttpBindingMethod.Get:
                {
                    string uri = AppendQuery(Target, content);
                    response = Client.GetAsync(uri, token);
                    break;
                }

                case HttpBindingMethod.Delete:
                {
                    string uri = AppendQuery(Target, content);
                    response = Client.DeleteAsync(uri, token);
                    break;
                }

                case HttpBindingMethod.Post:
                    response = Client.PostAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"), token);
                    break;

                case HttpBindingMethod.Put:
                    response = Client.PutAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"), token);
                    break;

                case HttpBindingMethod.Patch:
                    response = Client.PatchAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"), token);
                    break;
            }

            if (response == null)
                return false;

            return await ProcessResponse(response);
        }
        catch (Exception e)
        {
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:Http, Router:{Router.Name}, Binding:{Name}", e);
            return false;
        }
    }

    /// <summary>
    /// Appends the message content to the target as its query string.
    /// The content is the query string itself ("a=1&amp;b=2"), so it is not encoded.
    /// A target that already has a query string gets the content after "&amp;".
    /// </summary>
    private static string AppendQuery(string target, string content)
    {
        if (string.IsNullOrEmpty(content))
            return target;

        if (target.IndexOf('?') < 0)
            return target + "?" + content;

        return target.EndsWith('?') || target.EndsWith('&')
            ? target + content
            : target + "&" + content;
    }

    /// <summary>
    /// Waits for response and checks response status code.
    /// Ok Accepted and Created responses return true, others return false
    /// </summary>
    private static async Task<bool> ProcessResponse(Task<HttpResponseMessage> task)
    {
        try
        {
            using HttpResponseMessage response = await task;
            return response != null &&
                   (response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.Accepted ||
                    response.StatusCode == HttpStatusCode.Created);
        }
        catch
        {
            return false;
        }
    }
}
