using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Twino.Core;

namespace Twino.Mvc.Results
{
    /// <summary>
    /// JSON Action result
    /// </summary>
    public class JsonResult : IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        public HttpStatusCode Code { get; set; }

        /// <summary>
        /// Result content type (such as application/json, text/xml, text/plain)
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Result content body
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        /// <summary>
        /// JSON Serialization options.
        /// If null, system default is used.
        /// </summary>
        public JsonSerializationOptions Options { get; set; }

        /// <summary>
        /// Creates new empty JSON result
        /// </summary>
        public JsonResult(HttpStatusCode code = HttpStatusCode.OK)
        {
            Code = code;
            ContentType = ContentTypes.APPLICATION_JSON;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Creates new JSON result
        /// </summary>
        public JsonResult(object model, HttpStatusCode code = HttpStatusCode.OK)
        {
            Code = code;
            ContentType = ContentTypes.APPLICATION_JSON;
            Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Set(model);
        }

        /// <summary>
        /// Sets json objects of json result
        /// </summary>
        public async Task SetAsync(object model)
        {
            if (Options.UseNewtonsoft)
            {
                string serialized = Options.NewtonsoftOptions != null
                                        ? JsonConvert.SerializeObject(model)
                                        : JsonConvert.SerializeObject(model, Options.NewtonsoftOptions);

                Stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
            }
            else
            {
                Stream = new MemoryStream();
                await System.Text.Json.JsonSerializer.SerializeAsync(Stream, model, model.GetType(), Options.SystemTextOptions);
            }
        }

        /// <summary>
        /// Sets json objects of json result
        /// </summary>
        public void Set(object model)
        {
            if (Options.UseNewtonsoft)
            {
                string serialized = Options.NewtonsoftOptions != null
                                        ? JsonConvert.SerializeObject(model)
                                        : JsonConvert.SerializeObject(model, Options.NewtonsoftOptions);

                Stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
            }
            else
            {
                string serialized = System.Text.Json.JsonSerializer.Serialize(model, model.GetType(), Options.SystemTextOptions);
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
            }
        }
    }
}