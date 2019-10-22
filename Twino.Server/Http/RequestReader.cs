using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    /// <summary>
    /// HTTP Request reader class.
    /// Reads the http request
    /// </summary>
    public class RequestReader
    {
        #region Fields

        /// <summary>
        /// Request buffer
        /// </summary>
        private readonly byte[] _buffer = new byte[512];

        /// <summary>
        /// reqeust stream. When the data is read from socket buffer, it's written to this stream and is proceed from here
        /// </summary>
        private readonly MemoryStream _ms = new MemoryStream();

        /// <summary>
        /// Request Server
        /// </summary>
        private readonly TwinoServer _server;

        /// <summary>
        /// Reads string data from memory stream. Used for reading header values.
        /// </summary>
        private StreamReader _reader;

        /// <summary>
        /// Server options
        /// </summary>
        private readonly ServerOptions _options;

        /// <summary>
        /// Total read bytes from the socket for this request
        /// </summary>
        private int _totalRead;

        /// <summary>
        /// After reading some data from the socket and split it with enter (each line one header key value)
        /// Some data may excess. This data is kept in this field.
        /// when other package is received, this data will be append before the received value.
        /// </summary>
        private string _left;

        /// <summary>
        /// If true, request reader is reading header values.
        /// If false, reader is reading body content.
        /// </summary>
        private bool _readingHeader;

        /// <summary>
        /// Reading request
        /// </summary>
        private HttpRequest _request;

        /// <summary>
        /// Reading Request's response
        /// </summary>
        private HttpResponse _response;

        /// <summary>
        /// Request headers
        /// </summary>
        private readonly List<string> _headers = new List<string>();

        /// <summary>
        /// Request content memory stream
        /// </summary>
        private MemoryStream _body;

        /// <summary>
        /// Read bytes of body
        /// </summary>
        private int _bodyLength;

        /// <summary>
        /// If true, request reading is completed. If false, still reading.
        /// </summary>
        private bool _completed;

        /// <summary>
        /// Reading task completion. Will be set when the package fully read or an error occured while reading the package.
        /// </summary>
        private TaskCompletionSource<Tuple<HttpRequest, HttpResponse>> _completion;

        /// <summary>
        /// Info of the the
        /// </summary>
        private readonly HandshakeInfo _info;

        #endregion

        internal RequestReader(TwinoServer server, HandshakeInfo info)
        {
            _info = info;
            _server = server;
            _options = server.Options;
        }

        #region Actions

        /// <summary>
        /// Ends reading from the TCP socket
        /// </summary>
        private async Task<bool> ProcessRead(int read)
        {
            try
            {
                if (read == 0)
                {
                    _completion.SetResult(new Tuple<HttpRequest, HttpResponse>(null, null));
                    return false;
                }

                if (_readingHeader)
                {
                    _ms.Write(_buffer, 0, read);
                    _ms.Position -= read;
                }

                _totalRead += read;
                await ProcessBuffer(read);

                if (!_completed)
                    return true;
            }
            catch (Exception ex)
            {
                //if the object is disposed, it means server closed the connection. we do not need to log this
                if (!(ex is ObjectDisposedException) && _server.Logger != null)
                    _server.Logger.LogException("END_READ_REQUEST", ex);

                _completion.SetResult(new Tuple<HttpRequest, HttpResponse>(null, null));
            }

            return false;
        }

        /// <summary>
        /// After some byte data received while reading the request,
        /// Gets the data from the buffer and process
        /// </summary>
        private async Task ProcessBuffer(int read)
        {
            if (_readingHeader)
            {
                await ReadHeader();

                if (_totalRead > _options.MaximumHeaderLength)
                {
                    await WriteStatusResponse(_response, HttpStatusCode.RequestHeaderFieldsTooLarge);
                    return;
                }

                if (_totalRead > _options.MaximumRequestLength)
                {
                    await WriteStatusResponse(_response, HttpStatusCode.RequestEntityTooLarge);
                    return;
                }

                if (!_readingHeader && !_completed)
                    await ReadBody(0);
            }
            else
                await ReadBody(read);
        }

        /// <summary>
        /// Reads request header.
        /// Calculates bytes and if header is finished or not.
        /// </summary>
        private async Task ReadHeader()
        {
            string data = _reader.ReadToEnd();

            //if some data left from previous buffer, add this data
            if (!string.IsNullOrEmpty(_left))
                data = _left + data;

            //starting from 0 pointer and searching first "\n"
            //if we can't find, there is no line in this data, keep data in _left
            //_left will be added when next buffer arrived
            int start = 0;
            int i = data.IndexOf('\n', start);
            if (i < 0)
            {
                _left = data;
                return;
            }

            //line '\n' found
            while (i >= 0)
            {
                //if the line ends with \r, reduce length as 1 byte, we don't wanna see \r :)
                int len = i - start;
                if (data[start + len - 1] == '\r')
                    len--;

                //the header line
                string line = data.Substring(start, len);
                if (string.IsNullOrEmpty(line))
                {
                    //header is finished.
                    //we will read the content. but first, we need to analyze the request and find length if there is content.

                    _readingHeader = false;
                    _body = new MemoryStream();
                    await AnalyzeHeader();

                    if (!_completed)
                    {
                        int bstart = start + len + 2; //+2: second line "\r\n"
                        if (data.Length > bstart)
                        {
                            string bleft = data.Substring(bstart);
                            byte[] bdata = Encoding.UTF8.GetBytes(bleft);
                            _bodyLength = bdata.Length;
                            _body.Write(bdata, 0, bdata.Length);
                        }
                        else
                            _bodyLength = 0;
                    }

                    return;
                }

                _headers.Add(line);

                //line is read, we will search another line and start index is (last \n + 1)
                start = i + 1;
                if (start >= data.Length)
                    break;

                //find next \n
                i = data.IndexOf('\n', start);
            }

            //if there is data after last \n, we need to keep this data for next buffer receive
            _left = start < data.Length ? data.Substring(start) : null;
        }

        /// <summary>
        /// Reads request content
        /// </summary>
        private async Task ReadBody(int read)
        {
            if (read > 0)
            {
                await _body.WriteAsync(_buffer, 0, read);
                _bodyLength += read;
            }

            if (_bodyLength >= _request.ContentLength)
            {
                _request.Content = Encoding.UTF8.GetString(_body.ToArray());
                _completed = true;
                _completion.SetResult(new Tuple<HttpRequest, HttpResponse>(_request, _response));
            }
        }

        /// <summary>
        /// After request header is read, this method analyzes the header values.
        /// Creates the request and validates it
        /// </summary>
        private async Task AnalyzeHeader()
        {
            RequestBuilder builder = new RequestBuilder();
            _request = builder.Build(_headers.ToArray());
            _request.Response = _response;

            if (!await ValidateRequest(_request, _response))
                return;

            //we don't need to validate body ?
            //char[] body = new char[_request.ContentLength];

            _request.IsHttps = _info.Server.Options.SslEnabled;

            if (string.IsNullOrEmpty(_request.AcceptEncoding))
                _response.ContentEncoding = ContentEncodings.None;

            if (!string.IsNullOrEmpty(_options.ContentEncoding))
            {
                if (_options.ContentEncoding.Contains("br"))
                    _response.ContentEncoding = ContentEncodings.Brotli;
                else if (_options.ContentEncoding.Contains("gzip"))
                    _response.ContentEncoding = ContentEncodings.Gzip;
            }

            if (_request.ContentLength > 0)
                _readingHeader = false;
            else
            {
                _completed = true;
                _completion.SetResult(new Tuple<HttpRequest, HttpResponse>(_request, _response));
            }
        }

        /// <summary>
        /// Reads the HTTP Request from the stream and calls callback method when finished.
        /// </summary>
        public async Task<Tuple<HttpRequest, HttpResponse>> Read(Stream stream)
        {
            _completion = new TaskCompletionSource<Tuple<HttpRequest, HttpResponse>>(TaskCreationOptions.None);
            _reader = new StreamReader(_ms, Encoding.UTF8, true);
            _readingHeader = true;
            _totalRead = 0;
            _response = new HttpResponse();
            _response.Stream = stream;

            try
            {
                int x = 0;
                bool again;
                do
                {
                    int read = await stream.ReadAsync(_buffer, 0, _buffer.Length);
                    again = await ProcessRead(read);
                    x++;

                    if (x > short.MaxValue)
                        again = false;
                } while (again);
            }
            catch (Exception ex)
            {
                if (_info.State != ConnectionStates.Closed)
                {
                    _server.Logger.LogException("READ_REQUEST", ex);
                    _info.Close();
                }

                _completion.SetResult(new Tuple<HttpRequest, HttpResponse>(null, null));
            }

            return await _completion.Task;
        }

        /// <summary>
        /// Validates request headers, content, header and entity lengths
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ValidateRequest(HttpRequest request, HttpResponse response)
        {
            //check content length
            if (request.ContentLength > _options.MaximumRequestLength)
            {
                await WriteStatusResponse(response, HttpStatusCode.RequestEntityTooLarge);
                return false;
            }

            //content length required
            if (!request.ContentLengthSpecified && (request.Method == HttpHeaders.HTTP_POST || request.Method == HttpHeaders.HTTP_PUT || request.Method == HttpHeaders.HTTP_PATCH))
            {
                await WriteStatusResponse(response, HttpStatusCode.LengthRequired);
                return false;
            }

            //checks if hostname is allowed
            if (_info.Server.Options.Hostnames != null)
            {
                if (!(_info.Server.Options.Hostnames.Length == 1 && _info.Server.Options.Hostnames[0] == "*"))
                {
                    string host = request.Host.ToLower(new CultureInfo("en-US"));
                    bool allowed = false;
                    foreach (string ah in _info.Server.Options.Hostnames)
                    {
                        if (string.Equals(host, ah, StringComparison.InvariantCultureIgnoreCase))
                        {
                            allowed = true;
                            break;
                        }
                    }

                    if (!allowed)
                    {
                        await WriteStatusResponse(response, HttpStatusCode.BadGateway);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Writes status response end calls callback method with failed null values
        /// </summary>
        private async Task WriteStatusResponse(HttpResponse response, HttpStatusCode code)
        {
            response.StatusCode = code;
            ResponseWriter writer = new ResponseWriter(response, _server);
            await writer.Write(response);
        }

        #endregion
    }
}