using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    public class HttpReader
    {
        public const byte CR = (byte) '\r';
        public const byte LF = (byte) '\n';
        public const byte COLON = (byte) ':';
        public const byte SEMICOLON = (byte) ';';
        public const byte QMARK = (byte) '?';
        public const byte AND = (byte) '&';
        public const byte EQUALS = (byte) '=';
        public const byte SPACE = (byte) ' ';
        public static readonly byte[] CRLF = {CR, LF};
        public static readonly byte[] COLON_SPACE = {COLON, SPACE};

        private bool _firstLine = true;
        private bool _readingHeaders = true;
        private readonly byte[] _smallBuffer = new byte[256];
        
        //isn't initialized now, maybe we never require it
        private byte[] _largeBuffer;

        public int HeaderLength { get; private set; }
        public int ContentLength { get; private set; }

        private readonly ServerOptions _options;
        private readonly HostOptions _hostOptions;

        public HttpReader(ServerOptions options, HostOptions hostOptions)
        {
            _options = options;
            _hostOptions = hostOptions;
        }

        public void Reset()
        {
            _firstLine = true;
            _readingHeaders = true;
            HeaderLength = 0;
            ContentLength = 0;
        }

        public async Task<Tuple<HttpRequest, HttpResponse>> Read(Stream stream)
        {
            HttpRequest request = new HttpRequest();
            HttpResponse response = new HttpResponse();

            request.Response = response;
            response.NetworkStream = stream;

            int readLength = 0;
            int start = 0;

            //this value will be true, if small buffer isn't enough and tells us to use large buffer
            bool requiredMoreData = false;

            //when large buffer is used, this value will be true. and if we can't find CRLF even this value is true, bad request will be returned
            bool largeBufferUsed = false;

            do
            {
                byte[] buffer;

                //if end of the request reading
                if (!_readingHeaders && ContentLength >= request.ContentLength)
                    break;

                if (requiredMoreData)
                {
                    //we can't find CRLF with large buffer, return bad request
                    if (largeBufferUsed)
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        break;
                    }

                    //we need to keep data between start and end, read more data and put new data to the end of the kept

                    if (_largeBuffer == null)
                        _largeBuffer = new byte[6144]; //4096 for max cookie size, others for "Cookie:" key and cookie path and deadline (6KB total)

                    //copy left data to large buffer
                    int prevSize = readLength - start;
                    Buffer.BlockCopy(_smallBuffer, start, _largeBuffer, 0, prevSize);

                    //read more
                    int read = await stream.ReadAsync(_largeBuffer, prevSize, _largeBuffer.Length - prevSize);

                    readLength = prevSize + read;
                    buffer = _largeBuffer;
                    largeBufferUsed = true;
                }
                else
                {
                    readLength = await stream.ReadAsync(_smallBuffer, 0, _smallBuffer.Length);
                    if (readLength < 1 && _firstLine)
                        return new Tuple<HttpRequest, HttpResponse>(null, null);

                    buffer = _smallBuffer;
                    largeBufferUsed = false;
                }

                if (readLength < 1)
                    break;

                start = 0;
                while (start < readLength)
                {
                    //if reading header is finished, redirect to content reading
                    if (!_readingHeaders)
                    {
                        start = ReadContent(request, buffer, start, readLength - start);
                        continue;
                    }

                    //reading first line for http method, url and versio
                    if (_firstLine)
                    {
                        start = ReadRequestInfo(request, buffer, readLength - start, out requiredMoreData);

                        //reading first line isn't completed, we need to feed data and re-read first line
                        if (!requiredMoreData)
                            _firstLine = false;
                    }

                    //reading headers
                    else
                        start = ReadHeaderLine(request, buffer, start, readLength - start, out requiredMoreData);

                    if (requiredMoreData)
                        break;

                    //reading header is completed
                    if (!_readingHeaders)
                    {
                        bool ok = AnalyzeHeaders(request, response);
                        if (!ok)
                            return new Tuple<HttpRequest, HttpResponse>(request, response);
                    }
                }
            } while (readLength > 0);

            return new Tuple<HttpRequest, HttpResponse>(request, response);
        }

        /// <summary>
        /// Process first line of http request [METHOD URL VERSION]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadRequestInfo(HttpRequest request, byte[] buffer, int length, out bool requiredMoreData)
        {
            ReadOnlySpan<byte> crlf = new ReadOnlySpan<byte>(CRLF);

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
            int index = span.IndexOf(SPACE);
            Span<byte> method = span.Slice(0, index);
            request.Method = Encoding.UTF8.GetString(method);
            span = span.Slice(index + 1);

            //read url
            index = span.IndexOf(SPACE);
            Span<byte> url = span.Slice(0, index);

            int qmarkIndex = url.IndexOf(QMARK);

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

            ReadOnlySpan<byte> crlf = new ReadOnlySpan<byte>(CRLF);
            int endOfLine = span.IndexOf(crlf);

            //if buffer doesn't contain CRLF, we need load more data and come back here again 
            if (endOfLine < 0)
            {
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
            int index = span.IndexOf(COLON);
            Span<byte> key = span.Slice(0, index);

            //trim value (left)
            index++;
            while (span[index] == SPACE && index < endOfLine)
                index++;

            //trim value (right)
            int valueLength = endOfLine - index;
            int last = span.Length - 1;
            while (span[last] == SPACE && valueLength > 0)
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
        private int ReadContent(HttpRequest request, byte[] buffer, int start, int length)
        {
            if (request.ContentStream == null)
                request.ContentStream = new MemoryStream();

            request.ContentStream.Write(buffer, start, length);
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
            if (request.ContentLength > _options.MaximumRequestLength)
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
            if (_hostOptions.Hostnames != null)
            {
                if (!(_hostOptions.Hostnames.Length == 1 && _hostOptions.Hostnames[0] == "*"))
                {
                    string host = request.Host.ToLower(new CultureInfo("en-US"));
                    bool allowed = false;
                    foreach (string ah in _hostOptions.Hostnames)
                    {
                        if (string.Equals(host, ah, StringComparison.InvariantCultureIgnoreCase))
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

                else if (key.Equals(HttpHeaders.WEBSOCKET_KEY, StringComparison.InvariantCultureIgnoreCase))
                {
                    request.WebSocketKey = value;
                    request.IsWebSocket = true;
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
            }
        }
    }
}