using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

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
    /// Sends message to target as HTTP request and waits for response.
    /// Ok Accepted and Created responses return true, others return false
    /// </summary>
    public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        try
        {
            HttpClient client = new HttpClient();

            // ReSharper disable once PossibleInvalidOperationException
            HttpBindingMethod method = (HttpBindingMethod) ContentType.Value;
            string content = message.Length > 0 ? message.ToString() : null;
            Task<HttpResponseMessage> response = null;

            switch (method)
            {
                case HttpBindingMethod.Get:
                {
                    string uri = Target;
                    if (!string.IsNullOrEmpty(content))
                        uri += "?" + content;

                    response = client.GetAsync(uri);
                    break;
                }

                case HttpBindingMethod.Delete:
                {
                    string uri = Target;
                    if (!string.IsNullOrEmpty(content))
                        uri += "?" + content;

                    response = client.DeleteAsync(uri);
                    break;
                }

                case HttpBindingMethod.Post:
                    response = client.PostAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"));
                    break;

                case HttpBindingMethod.Put:
                    response = client.PutAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"));
                    break;

                case HttpBindingMethod.Patch:
                    response = client.PatchAsync(Target, new StringContent(content ?? "", Encoding.UTF8, "application/json"));
                    break;
            }

            if (response == null)
                return false;

            return await ProcessResponse(response);
        }
        catch (Exception e)
        {
            Router.Rider.SendError("BINDING_SEND", e, $"Type:Http, Binding:{Name}");
            return false;
        }
    }

    /// <summary>
    /// Waits for response and checks response status code.
    /// Ok Accepted and Created responses return true, others return false
    /// </summary>
    private async Task<bool> ProcessResponse(Task<HttpResponseMessage> task)
    {
        try
        {
            HttpResponseMessage response = await task;
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