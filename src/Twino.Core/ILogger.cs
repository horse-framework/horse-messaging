using System;

namespace Twino.Core
{
    /// <summary>
    /// Error Logging implementation for HttpServer, ClientSocket, ServerSocket, Firewall, ClientContainer and HttpRequestHandler objects
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs the error
        /// </summary>
        void LogException(string hint, Exception exception);

        /// <summary>
        /// Logs an event
        /// </summary>
        void LogEvent(string hint, string message);
    }
}