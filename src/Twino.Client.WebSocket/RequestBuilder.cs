using System;
using System.IO;
using System.Text;
using Twino.Protocols.Http;

namespace Twino.Client.WebSocket
{
    /// <summary>
    /// Reads full string request data and creates new HttpRequest object
    /// </summary>
    internal class RequestBuilder
    {
        /// <summary>
        /// Builds full string request data and creates new HttpRequest object
        /// </summary>
        public HttpRequest Build(string[] lines)
        {
            HttpRequest request = new HttpRequest();
            request.ContentStream = new MemoryStream();

            //string[] lines = data.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            bool head = true;

            //read first line. Must be in "GET path HTTP/version" format
            string[] headline = lines[0].Split(' ');

            if (headline.Length < 2)
                return null;

            request.Method = headline[0];
            request.Path = headline[1];

            //reads content and header data
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (head && string.IsNullOrEmpty(line))
                {
                    head = false;
                    continue;
                }

                if (head)
                {
                    int index = line.IndexOf(':');
                    if (index < 0)
                        continue;

                    string key = line.Substring(0, index);

                    if (line[index + 1] == ' ')
                        index++;

                    string value = line.Substring(index + 1);
                    AddHeader(request, key, value);
                }
                else
                {
                    string sdata = (i + 1 == lines.Length) ? line : (line + "\r\n");
                    byte[] bdata = Encoding.UTF8.GetBytes(sdata);
                    request.ContentStream.Write(bdata, 0, bdata.Length);
                }
            }

            return request;
        }

        /// <summary>
        /// Adds header key and value to the requests.
        /// If the key is member of the requests (as property)
        /// The property is set, otherwise it's added to Headers dictionary
        /// </summary>
        private void AddHeader(HttpRequest request, string key, string value)
        {
            key = key.Trim();
            value = value.Trim();

            if (key.Equals(HttpHeaders.HOST, StringComparison.InvariantCultureIgnoreCase))
                request.Host = value;

            else if (key.Equals(HttpHeaders.WEBSOCKET_KEY, StringComparison.InvariantCultureIgnoreCase))
            {
                request.WebSocketKey = value;
                request.Upgrade = "websocket";
            }

            else if (key.Equals(HttpHeaders.ACCEPT_ENCODING, StringComparison.InvariantCultureIgnoreCase))
                request.AcceptEncoding = value;

            else if (key.Equals(HttpHeaders.CONTENT_TYPE, StringComparison.InvariantCultureIgnoreCase))
                request.ContentType = value;

            else if (key.Equals(HttpHeaders.CONTENT_LENGTH, StringComparison.InvariantCultureIgnoreCase))
            {
                request.ContentLength = Convert.ToInt32(value);
                request.ContentLengthSpecified = true;
            }

            else
                request.Headers.Add(key, value);
        }
    }
}