using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Models
{
    /// <summary>
    /// 
    /// </summary>
    public class HorseModelResult
    {
        /// <summary>
        /// Response message result
        /// </summary>
        public HorseResult Result { get; set; }

        /// <summary>
        /// True, if response code is Ok "200"
        /// </summary>
        public bool Ok => Result.Code == HorseResultCode.Ok;

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public HorseModelResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public HorseModelResult(HorseResult result)
        {
            Result = result;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static HorseModelResult FromContentType(ushort code)
        {
            return new HorseModelResult(new HorseResult((HorseResultCode) code));
        }
    }

    /// <inheritdoc cref="HorseModelResult" />
    public class HorseModelResult<TModel>
    {
        /// <summary>
        /// Response message result
        /// </summary>
        public HorseResult Result { get; set; }

        /// <summary>
        /// Response model
        /// </summary>
        public TModel Model { get; set; }

        /// <summary>
        /// Create new empty result object
        /// </summary>
        public HorseModelResult()
        {
        }

        /// <summary>
        /// Creates new result object from response code
        /// </summary>
        public HorseModelResult(HorseResult result)
        {
            Result = result;
        }

        /// <summary>
        /// Creates new result object from response code and model
        /// </summary>
        public HorseModelResult(HorseResult result, TModel model)
        {
            Result = result;
            Model = model;
        }

        /// <summary>
        /// Creates new result object from content type
        /// </summary>
        public static HorseModelResult<TModel> FromContentType(ushort code)
        {
            return new HorseModelResult<TModel>(new HorseResult((HorseResultCode) code));
        }
    }
}