using Twino.Mvc.Controllers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using Twino.Core;

namespace Twino.Mvc.Results
{
    /// <summary>
    /// XML Action result
    /// </summary>
    public class XmlResult : IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        public HttpStatusCode Code { get; set; }

        /// <summary>
        /// Result content type (such as application/json, text/xml, text/plain)
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// Result content body
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        public XmlResult(object obj)
        {
            Code = HttpStatusCode.OK;
            ContentType = ContentTypes.TEXT_XML;
            Headers = new Dictionary<string, string>();

            XmlSerializer serializer = new XmlSerializer(obj.GetType());
            using StringWriter writer = new StringWriter();
            using XmlWriter xml = XmlWriter.Create(writer);
            serializer.Serialize(xml, obj);
            Content = xml.ToString();
        }

    }
}
