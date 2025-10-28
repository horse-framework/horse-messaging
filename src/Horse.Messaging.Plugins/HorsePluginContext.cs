using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

/// <summary>
/// Plugin context object for plugin events
/// </summary>
public class HorsePluginContext
{
    /// <summary>
    /// Context source event
    /// </summary>
    public HorsePluginEvent SourceEvent { get; }

    /// <summary>
    /// Plugin instance
    /// </summary>
    public HorsePlugin Plugin { get; }

    /// <summary>
    /// Request message
    /// </summary>
    public HorseMessage Request { get; }

    /// <summary>
    /// Response message
    /// </summary>
    public HorseMessage Response { get; set; }

    /// <summary>
    /// Plugin rider object for access to server functions.
    /// </summary>
    public IPluginRider Rider { get; }

    internal HorsePluginContext(HorsePluginEvent sourceEvent, HorsePlugin plugin, IPluginRider rider, HorseMessage request)
    {
        SourceEvent = sourceEvent;
        Plugin = plugin;
        Request = request;
        Rider = rider;
    }

    /// <summary>
    /// Reads content as json model
    /// </summary>
    public ValueTask<T> ReadAsJson<T>(JsonSerializerOptions options = null) where T : class, new()
    {
        Request.Content.Position = 0;
        return JsonSerializer.DeserializeAsync<T>(Request.Content, options);
    }

    /// <summary>
    /// Writes response message from json model
    /// </summary>
    public void WriteJsonResponse<T>(T model, JsonSerializerOptions options = null)
    {
        Response = Request.CreateResponse(HorseResultCode.Ok);
        Response.Content = new MemoryStream();
        JsonSerializer.Serialize(Response.Content, model, options);
    }
}