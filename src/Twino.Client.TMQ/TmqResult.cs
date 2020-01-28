using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// 
    /// </summary>
    public class TmqResult
    {
        /// <summary>
        /// Operation response code
        /// </summary>
        public TmqResponseCode ResponseCode { get; set; }

        /// <summary>
        /// True, if response code is Ok "200"
        /// </summary>
        public bool Ok => ResponseCode == TmqResponseCode.Ok;

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public TmqResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public TmqResult(TmqResponseCode code)
        {
            ResponseCode = code;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static TmqResult FromContentType(ushort code)
        {
            return new TmqResult((TmqResponseCode) code);
        }
    }

    /// <inheritdoc cref="TmqResult" />
    public class TmqResult<TModel>
    {
        /// <inheritdoc cref="ResponseCode" />
        public TmqResponseCode ResponseCode { get; set; }

        /// <summary>
        /// Response model
        /// </summary>
        public TModel Model { get; set; }

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public TmqResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public TmqResult(TmqResponseCode code)
        {
            ResponseCode = code;
        }

        /// <summary>
        /// Creates new result object from response code and model
        /// </summary>
        public TmqResult(TmqResponseCode code, TModel model)
        {
            ResponseCode = code;
            Model = model;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static TmqResult<TModel> FromContentType(ushort code)
        {
            return new TmqResult<TModel>((TmqResponseCode) code);
        }
    }
}