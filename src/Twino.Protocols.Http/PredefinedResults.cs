using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Predefined default status code response content messages for twino server
    /// </summary>
    internal static class PredefinedResults
    {
        private const string B = "<html><title></title><body style=\"text-align:center;\"><div style=\"padding:30px;\"><h1>";
        private const string A = "</h1></div><hr /><span>twino</span></body></html>";

        internal static readonly Dictionary<HttpStatusCode, byte[]> Statuses
            = new Dictionary<HttpStatusCode, byte[]>
              {
                  {HttpStatusCode.OK, Encoding.UTF8.GetBytes(B + "Ok" + A)},
                  {HttpStatusCode.Created, Encoding.UTF8.GetBytes(B + "Created" + A)},
                  {HttpStatusCode.Accepted, Encoding.UTF8.GetBytes(B + "Accepted" + A)},
                  {HttpStatusCode.NonAuthoritativeInformation, Encoding.UTF8.GetBytes(B + "Non Authoritative Information" + A)},
                  {HttpStatusCode.Found, Encoding.UTF8.GetBytes(B + "Object Moved" + A)},
                  {HttpStatusCode.BadRequest, Encoding.UTF8.GetBytes(B + "Bad Request" + A)},
                  {HttpStatusCode.Unauthorized, Encoding.UTF8.GetBytes(B + "Unauthorized" + A)},
                  {HttpStatusCode.Forbidden, Encoding.UTF8.GetBytes(B + "Forbidden" + A)},
                  {HttpStatusCode.NotFound, Encoding.UTF8.GetBytes(B + "Not Found" + A)},
                  {HttpStatusCode.MethodNotAllowed, Encoding.UTF8.GetBytes(B + "Method Not Allowed" + A)},
                  {HttpStatusCode.NotAcceptable, Encoding.UTF8.GetBytes(B + "Not Acceptable" + A)},
                  {HttpStatusCode.RequestTimeout, Encoding.UTF8.GetBytes(B + "Request Timeout" + A)},
                  {HttpStatusCode.Conflict, Encoding.UTF8.GetBytes(B + "Conflict" + A)},
                  {HttpStatusCode.Gone, Encoding.UTF8.GetBytes(B + "Gone" + A)},
                  {HttpStatusCode.RequestEntityTooLarge, Encoding.UTF8.GetBytes(B + "Request Entity Too Large" + A)},
                  {HttpStatusCode.UnsupportedMediaType, Encoding.UTF8.GetBytes(B + "Unsupported Media Type" + A)},
                  {HttpStatusCode.ExpectationFailed, Encoding.UTF8.GetBytes(B + "Expectation Failed" + A)},
                  {HttpStatusCode.Locked, Encoding.UTF8.GetBytes(B + "Locked" + A)},
                  {HttpStatusCode.PreconditionRequired, Encoding.UTF8.GetBytes(B + "Precondition Required" + A)},
                  {HttpStatusCode.TooManyRequests, Encoding.UTF8.GetBytes(B + "Too Many Requests" + A)},
                  {HttpStatusCode.RequestHeaderFieldsTooLarge, Encoding.UTF8.GetBytes(B + "Request HeaderFields Too Large" + A)},
                  {HttpStatusCode.InternalServerError, Encoding.UTF8.GetBytes(B + "Internal Server Error" + A)},
                  {HttpStatusCode.NotImplemented, Encoding.UTF8.GetBytes(B + "Not Implemented" + A)},
                  {HttpStatusCode.BadGateway, Encoding.UTF8.GetBytes(B + "Bad Gateway" + A)},
                  {HttpStatusCode.ServiceUnavailable, Encoding.UTF8.GetBytes(B + "Service Unavailable" + A)},
              };
    }
}