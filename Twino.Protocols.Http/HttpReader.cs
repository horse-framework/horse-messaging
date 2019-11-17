using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;
using Twino.Protocols.Http.Forms;
using Twino.Server.Http;

namespace Twino.Protocols.Http
{
    public class HttpReader : IProtocolMessageReader<HttpMessage>
    {
        #region Fields - Properties

        /// <summary>
        /// If true, the first line is being read
        /// </summary>
        private bool _firstLine = true;

        /// <summary>
        /// If true, reader is reading headers from network stream. Otherwise content is being read.
        /// </summary>
        private bool _readingHeaders = true;

        /// <summary>
        /// Network stream read buffer
        /// </summary>
        private readonly byte[] _buffer = new byte[256];

        /// <summary>
        /// Request stream, buffer is writted to this stream while reading from network stream
        /// </summary>
        private readonly MemoryStream _stream = new MemoryStream(256);

        /// <summary>
        /// Is kept for checking if header length exceeds maximum header length option
        /// </summary>
        public int HeaderLength { get; private set; }

        /// <summary>
        /// Is kept for recognize content length
        /// </summary>
        public int ContentLength { get; private set; }

        private readonly HttpOptions _options;

        public ProtocolHandshakeResult HandshakeResult { get; set; }

        #endregion

        public HttpReader(HttpOptions options)
        {
            _options = options;
        }

        public void Reset()
        {
            _firstLine = true;
            _readingHeaders = true;
            HeaderLength = 0;
            ContentLength = 0;
            _stream.Position = 0;
            _stream.SetLength(0);
        }

        /// <summary>
        /// Reads, parses and creates request from stream and creates response for the request
        /// </summary>
        public async Task<HttpMessage> Read(Stream stream)
        {
            HttpRequest request = new HttpRequest();
            HttpResponse response = new HttpResponse();

            request.Response = response;
            response.NetworkStream = stream;

            int readLength;
            int start = 0;

            //this value will be true, if small buffer isn't enough and tells us to use large buffer
            bool requiredMoreData = false;

            do
            {
                //if end of the request reading
                if (!_readingHeaders && ContentLength >= request.ContentLength)
                    break;

                if (HandshakeResult.ReadAfter && HandshakeResult.PreviouslyRead != null)
                {
                    HandshakeResult.PreviouslyRead.CopyTo(_buffer, 0);
                    readLength = await stream.ReadAsync(_buffer,
                                                        HandshakeResult.PreviouslyRead.Length,
                                                        _buffer.Length - HandshakeResult.PreviouslyRead.Length);
                    
                    HandshakeResult.PreviouslyRead = null;
                    HandshakeResult.ReadAfter = false;
                }
                else
                    readLength = await stream.ReadAsync(_buffer, 0, _buffer.Length);

                if (readLength < 1)
                    return null;

                if (requiredMoreData)
                {
                    await _stream.WriteAsync(_buffer, 0, readLength);
                    requiredMoreData = false;
                }
                else
                {
                    if (_stream.Position > 0)
                        _stream.Position = 0;

                    if (_stream.Length > readLength)
                        _stream.SetLength(readLength);

                    await _stream.WriteAsync(_buffer, 0, readLength);
                    start = 0;
                }

                if (readLength < 1)
                    break;

                //start = 0;
                while (start < _stream.Length)
                {
                    //if reading header is finished, redirect to content reading
                    if (!_readingHeaders)
                    {
                        start = await ReadContent(request, _stream.GetBuffer(), start, (int) _stream.Length - start);
                        continue;
                    }

                    //reading first line for http method, url and versio
                    if (_firstLine)
                    {
                        start = ReadRequestInfo(request, _stream.GetBuffer(), (int) _stream.Length, out requiredMoreData);

                        //reading first line isn't completed, we need to feed data and re-read first line
                        if (!requiredMoreData)
                            _firstLine = false;
                    }

                    //reading headers
                    else
                        start = ReadHeaderLine(request, _stream.GetBuffer(), start, (int) _stream.Length - start, out requiredMoreData);

                    if (requiredMoreData)
                        break;

                    //reading header is completed
                    if (!_readingHeaders)
                    {
                        bool ok = AnalyzeHeaders(request, response);
                        if (!ok)
                            return new HttpMessage(request, response);
                    }
                }
            } while (readLength > 0);

            return new HttpMessage(request, response);
        }

        /// <summary>
        /// Process first line of http request [METHOD URL VERSION]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadRequestInfo(HttpRequest request, byte[] buffer, int length, out bool requiredMoreData)
        {
            ReadOnlySpan<byte> crlf = new ReadOnlySpan<byte>(PredefinedMessages.CRLF);

            Span<byte> span = new Span<byte>(buffer, 0, length);
            int endOfLine = span.IndexOf(crlf);

            //if buffer doesn't contain CRLF, we need load more data and come back here again 
            if (endOfLine < 0)
            {
                //keep last read, read more and come here again
                requiredMoreData = true;
                return 0;
            }

            HeaderLength += endOfLine + 1;
            requiredMoreData = false;

            //read http method
            int index = span.IndexOf(PredefinedMessages.SPACE);
            Span<byte> method = span.Slice(0, index);
            request.Method = Encoding.UTF8.GetString(method);
            span = span.Slice(index + 1);

            //read url
            index = span.IndexOf(PredefinedMessages.SPACE);
            Span<byte> url = span.Slice(0, index);

            int qmarkIndex = url.IndexOf(PredefinedMessages.QMARK);

            //url has no querystring
            if (qmarkIndex < 0)
                request.Path = Encoding.UTF8.GetString(url);

            //url has querystring
            else
            {
                request.Path = Encoding.UTF8.GetString(url.Slice(0, qmarkIndex));
                request.QueryStringData = Encoding.UTF8.GetString(url.Slice(qmarkIndex + 1, url.Length - qmarkIndex - 1));
            }

            //dont check http version
            //8 magic number is; len("HTTP/1.1") = len("HTTP/2.0") = 8
            //Span<byte> version = span.Slice(0, 8);

            return endOfLine + 2;
        }

        /// <summary>
        /// Process single header line from buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadHeaderLine(HttpRequest request, byte[] buffer, int start, int length, out bool requiredMoreData)
        {
            Span<byte> span = new Span<byte>(buffer, start, length);

            ReadOnlySpan<byte> crlf = new ReadOnlySpan<byte>(PredefinedMessages.CRLF);
            int endOfLine = span.IndexOf(crlf);

            //if buffer doesn't contain CRLF, we need load more data and come back here again 
            if (endOfLine < 0)
            {
                //last empty line
                if (buffer[start] == PredefinedMessages.LF)
                {
                    requiredMoreData = false;
                    _readingHeaders = false;
                    return start + 1;
                }

                //keep last read, read more and come here again
                requiredMoreData = true;
                return start;
            }

            requiredMoreData = false;
            HeaderLength += endOfLine + 1;

            //this is blank line. end of header.
            if (endOfLine == 0)
            {
                _readingHeaders = false;
                return start + endOfLine + 2;
            }

            //read header key
            int index = span.IndexOf(PredefinedMessages.COLON);
            Span<byte> key = span.Slice(0, index);

            //trim value (left)
            index++;
            while (span[index] == PredefinedMessages.SPACE && index < endOfLine)
                index++;

            //trim value (right)
            int valueLength = endOfLine - index;
            int last = span.Length - 1;
            while (span[last] == PredefinedMessages.SPACE && valueLength > 0)
            {
                valueLength--;
                last--;
            }

            //read value
            Span<byte> value = span.Slice(index, endOfLine - index);

            //add header
            request.Headers.Add(Encoding.UTF8.GetString(key), Encoding.UTF8.GetString(value));

            return start + endOfLine + 2;
        }

        /// <summary>
        /// Read request content
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<int> ReadContent(HttpRequest request, byte[] buffer, int start, int length)
        {
            if (request.ContentStream == null)
                request.ContentStream = new MemoryStream();

            await request.ContentStream.WriteAsync(buffer, start, length);
            ContentLength += length;

            return start + length;
        }

        /// <summary>
        /// Check host name, request length, content length
        /// </summary>
        private bool AnalyzeHeaders(HttpRequest request, HttpResponse response)
        {
            SetKnownHeaders(request);

            //check content length
            if (_options.MaximumRequestLength > 0 && request.ContentLength > _options.MaximumRequestLength)
            {
                response.StatusCode = HttpStatusCode.RequestEntityTooLarge;
                return false;
            }

            //content length required
            if (!request.ContentLengthSpecified &&
                (request.Method == HttpHeaders.HTTP_POST ||
                 request.Method == HttpHeaders.HTTP_PUT ||
                 request.Method == HttpHeaders.HTTP_PATCH))
            {
                response.StatusCode = HttpStatusCode.LengthRequired;
                return false;
            }

            //checks if hostname is allowed
            if (_options.Hostnames != null)
            {
                if (!(_options.Hostnames.Length == 1 && _options.Hostnames[0] == "*"))
                {
                    bool allowed = false;
                    foreach (string ah in _options.Hostnames)
                    {
                        if (string.Equals(request.Host, ah, StringComparison.InvariantCultureIgnoreCase))
                        {
                            allowed = true;
                            break;
                        }
                    }

                    if (!allowed)
                    {
                        response.StatusCode = HttpStatusCode.BadGateway;
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Read headers and set request's properties
        /// </summary>
        private static void SetKnownHeaders(HttpRequest request)
        {
            foreach (var (key, value) in request.Headers)
            {
                if (key.Equals(HttpHeaders.HOST, StringComparison.InvariantCultureIgnoreCase))
                    request.Host = value;

                else if (key.Equals(HttpHeaders.ACCEPT_ENCODING, StringComparison.InvariantCultureIgnoreCase))
                    request.AcceptEncoding = value;

                else if (key.Equals(HttpHeaders.CONTENT_TYPE, StringComparison.InvariantCultureIgnoreCase))
                {
                    int i = value.IndexOf(';');
                    if (i < 1)
                        request.ContentType = value;
                    else
                    {
                        request.ContentType = value.Substring(0, i);
                        int partIndex = value.IndexOf('=', i + 2);
                        if (partIndex > 0)
                            request.Boundary = value.Substring(partIndex + 1);
                    }
                }

                else if (key.Equals(HttpHeaders.CONTENT_LENGTH, StringComparison.InvariantCultureIgnoreCase))
                {
                    request.ContentLength = Convert.ToInt32(value);
                    request.ContentLengthSpecified = true;
                }

                else if (key.Equals(HttpHeaders.WEBSOCKET_KEY, StringComparison.InvariantCultureIgnoreCase))
                {
                    request.WebSocketKey = value;
                    request.IsWebSocket = true;
                }
            }
        }

        /// <summary>
        /// Reads query string from header and form - file data from content, sets values to request's QueryString, Form and Files properties
        /// </summary>
        internal void ReadContent(HttpRequest request)
        {
            request.QueryString = !string.IsNullOrEmpty(request.QueryStringData)
                                      ? EncodedFormDataReader.Read(request.QueryStringData)
                                      : new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (string.IsNullOrEmpty(request.ContentType))
                return;

            if (request.ContentType.Equals(HttpHeaders.MULTIPART_FORM_DATA, StringComparison.InvariantCultureIgnoreCase))
            {
                if (request.ContentLength > 0)
                {
                    List<FormDataItem> data = MultipartFormDataReader.Read(request.Boundary, request.ContentStream);

                    request.Form = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    foreach (FormDataItem item in data.Where(x => !x.IsFile))
                    {
                        string value = Encoding.UTF8.GetString(item.Stream.ToArray());
                        if (request.Form.ContainsKey(item.Name))
                            request.Form[item.Name] += "," + value;
                        else
                            request.Form.Add(item.Name, value);
                    }

                    request.Files = data.Where(x => x.IsFile).Select(x => new FormFile
                                                                          {
                                                                              Filename = x.Filename,
                                                                              Name = x.Name,
                                                                              Size = Convert.ToInt32(x.Stream.Length),
                                                                              Stream = x.Stream,
                                                                              ContentType = x.ContentType
                                                                          });
                }
                else
                {
                    request.Form = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    request.Files = new IFormFile[0];
                }
            }
            else if (request.ContentType.Equals(HttpHeaders.APPLICATION_FORM_URLENCODED, StringComparison.InvariantCultureIgnoreCase))
            {
                request.Form = request.ContentLength > 0
                                   ? EncodedFormDataReader.Read(request.ContentStream)
                                   : new Dictionary<string, string>();
            }
        }
    }
}