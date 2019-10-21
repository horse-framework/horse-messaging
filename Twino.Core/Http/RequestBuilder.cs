using System;
using System.Collections.Generic;
using System.Globalization;

namespace Twino.Core.Http
{
    /// <summary>
    /// Reads full string request data and creates new HttpRequest object
    /// </summary>
    public class RequestBuilder
    {
        /// <summary>
        /// Builds full string request data and creates new HttpRequest object
        /// </summary>
        public HttpRequest Build(string[] lines)
        {
            HttpRequest request = new HttpRequest();
            request.Content = "";
            request.Headers = new Dictionary<string, string>();

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
                    string value = line.Substring(index + 1);
                    AddHeader(request, key.ToLower(CultureInfo.InvariantCulture), value);
                }
                else
                    request.Content += (i + 1 == lines.Length) ? line : (line + Environment.NewLine);
            }

            return request;
        }

        /// <summary>
        /// Builds HttpRequest from header lines.
        /// Used when request received from network as partial (header only)
        /// </summary>
        public HttpRequest Build(List<string> headers)
        {
            HttpRequest request = new HttpRequest();
            request.Headers = new Dictionary<string, string>();

            string[] headline = headers[0].Split(' ');

            if (headline.Length < 2)
                return null;

            request.Method = headline[0];
            request.Path = headline[1];

            foreach (string line in headers)
            {
                int index = line.IndexOf(':');
                if (index < 0)
                    continue;

                string key = line.Substring(0, index);
                string value = line.Substring(index + 1);
                AddHeader(request, key, value);
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
            string lcase = key.Trim().ToLower();
            string trimmed_value = value.Trim();

            switch (lcase)
            {
                case HttpHeaders.LCASE_HOST:
                    request.Host = trimmed_value;
                    break;

                case HttpHeaders.LCASE_WEBSOCKET_KEY:
                    request.WebSocketKey = trimmed_value;
                    request.IsWebSocket = true;
                    break;

                case HttpHeaders.LCASE_ACCEPT_ENCODING:
                    request.AcceptEncoding = trimmed_value;
                    break;

                case HttpHeaders.LCASE_CONTENT_TYPE:
                    request.ContentType = trimmed_value;
                    break;

                case HttpHeaders.LCASE_CONTENT_LENGTH:
                    request.ContentLength = Convert.ToInt32(trimmed_value);
                    request.ContentLengthSpecified = true;
                    break;

                default:
                    request.Headers.Add(key, trimmed_value);
                    break;
            }
        }
    }
}