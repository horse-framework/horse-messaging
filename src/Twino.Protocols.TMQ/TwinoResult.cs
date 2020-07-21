namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Twino Messagign Queue Operation Result
    /// </summary>
    public struct TwinoResult
    {
        /// <summary>
        /// Result code
        /// </summary>
        public TwinoResultCode Code;

        /// <summary>
        /// Reason for unsuccessful results
        /// </summary>
        public string Reason;

        /// <summary>
        /// Creates new result without reason
        /// </summary>
        public TwinoResult(TwinoResultCode code)
        {
            Code = code;
            Reason = null;
        }

        /// <summary>
        /// Creates new result with a reason.
        /// </summary>
        public TwinoResult(TwinoResultCode code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        /// <summary>
        /// Creates sucessful result with no reason
        /// </summary>
        public static TwinoResult Ok()
        {
            return new TwinoResult(TwinoResultCode.Ok);
        }

        /// <summary>
        /// Creates failed result with no reason
        /// </summary>
        public static TwinoResult Failed()
        {
            return new TwinoResult(TwinoResultCode.Failed);
        }
        
        /// <summary>
        /// Creates failed result with reason
        /// </summary>
        public static TwinoResult Failed(string reason)
        {
            return new TwinoResult(TwinoResultCode.Failed, reason);
        }
    }
}