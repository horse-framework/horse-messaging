using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class TmqResult
    {
        /// <summary>
        /// Response message result
        /// </summary>
        public TwinoResult Result { get; set; }

        /// <summary>
        /// True, if response code is Ok "200"
        /// </summary>
        public bool Ok => Result.Code == TwinoResultCode.Ok;

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public TmqResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public TmqResult(TwinoResult result)
        {
            Result = result;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static TmqResult FromContentType(ushort code)
        {
            return new TmqResult(new TwinoResult((TwinoResultCode) code));
        }
    }

    /// <inheritdoc cref="TmqResult" />
    public class TmqModelResult<TModel>
    {
        /// <summary>
        /// Response message result
        /// </summary>
        public TwinoResult Result { get; set; }

        /// <summary>
        /// Response model
        /// </summary>
        public TModel Model { get; set; }

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public TmqModelResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public TmqModelResult(TwinoResult result)
        {
            Result = result;
        }

        /// <summary>
        /// Creates new result object from response code and model
        /// </summary>
        public TmqModelResult(TwinoResult result, TModel model)
        {
            Result = result;
            Model = model;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static TmqModelResult<TModel> FromContentType(ushort code)
        {
            return new TmqModelResult<TModel>(new TwinoResult((TwinoResultCode) code));
        }
    }
}