using System;

namespace Horse.Mq
{
    /// <summary>
    /// Exception implementations for Horse MQ
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// Executed when an exception is thrown by the server
        /// </summary>
        /// <param name="hint">Error hint</param>
        /// <param name="exception">Exception</param>
        /// <param name="payload">Extra data about the error</param>
        void Error(string hint, Exception exception, string payload);
    }
}