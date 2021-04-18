namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// HorseClient process result enum
    /// </summary>
    public enum HorseResultCode : ushort
    {
        /// <summary>
        /// Operation succeeded
        /// </summary>
        Ok = 0,

        /// <summary>
        /// Unknown failed response
        /// </summary>
        Failed = 1,
        
        /// <summary>
        /// Request successfull but response has no content
        /// </summary>
        NoContent = 204,

        /// <summary>
        /// Request is not recognized or verified by the server
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Access denied for the operation
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// PaymentRequired = 402
        /// </summary>
        PaymentRequired = 402,

        /// <summary>
        /// Forbidden = 403
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// Target could not be found
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// MethodNotAllowed = 405
        /// </summary>
        MethodNotAllowed = 405,

        /// <summary>
        /// Request is not acceptable. Eg, queue status does not support the operation
        /// </summary>
        Unacceptable = 406,

        /// <summary>
        /// RequestTimeout = 408
        /// </summary>
        RequestTimeout = 408,

        /// <summary>
        /// Conflict = 409
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// Gone = 410
        /// </summary>
        Gone = 410,

        /// <summary>
        /// LengthRequired = 411
        /// </summary>
        LengthRequired = 411,

        /// <summary>
        /// PreconditionFailed = 412
        /// </summary>
        PreconditionFailed = 412,

        /// <summary>
        /// RequestEntityTooLarge = 413
        /// </summary>
        RequestEntityTooLarge = 413,

        /// <summary>
        /// RequestUriTooLong = 414
        /// </summary>
        RequestUriTooLong = 414,

        /// <summary>
        /// UnsupportedMediaType = 415
        /// </summary>
        UnsupportedMediaType = 415,

        /// <summary>
        /// RequestedRangeNotSatisfiable = 416
        /// </summary>
        RequestedRangeNotSatisfiable = 416,

        /// <summary>
        /// ExpectationFailed = 417
        /// </summary>
        ExpectationFailed = 417,

        /// <summary>
        /// MisdirectedRequest = 421
        /// </summary>
        MisdirectedRequest = 421,

        /// <summary>
        /// UnprocessableEntity = 422
        /// </summary>
        UnprocessableEntity = 422,

        /// <summary>
        /// Locked = 423
        /// </summary>
        Locked = 423,

        /// <summary>
        /// FailedDependency = 424
        /// </summary>
        FailedDependency = 424,

        /// <summary>
        /// UpgradeRequired = 426
        /// </summary>
        UpgradeRequired = 426,

        /// <summary>
        /// PreconditionRequired = 428
        /// </summary>
        PreconditionRequired = 428,

        /// <summary>
        /// TooManyRequests = 429
        /// </summary>
        TooManyRequests = 429,

        /// <summary>
        /// RequestHeaderFieldsTooLarge = 431
        /// </summary>
        RequestHeaderFieldsTooLarge = 431,

        /// <summary>
        /// UnavailableForLegalReasons = 451
        /// </summary>
        UnavailableForLegalReasons = 451,

        /// <summary>
        /// Requested data is already exists
        /// </summary>
        Duplicate = 481,

        /// <summary>
        /// Client, consumer, queue or message limit is exceeded
        /// </summary>
        LimitExceeded = 482,

        /// <summary>
        /// InternalServerError = 500
        /// </summary>
        InternalServerError = 500,

        /// <summary>
        /// NotImplemented = 501
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// BadGateway = 502
        /// </summary>
        BadGateway = 502,

        /// <summary>
        /// Target is busy to complete the process
        /// </summary>
        Busy = 503,

        /// <summary>
        /// GatewayTimeout = 504
        /// </summary>
        GatewayTimeout = 504,

        /// <summary>
        /// HttpVersionNotSupported = 505
        /// </summary>
        HttpVersionNotSupported = 505,

        /// <summary>
        /// VariantAlsoNegotiates = 506
        /// </summary>
        VariantAlsoNegotiates = 506,

        /// <summary>
        /// InsufficientStorage = 507
        /// </summary>
        InsufficientStorage = 507,

        /// <summary>
        /// LoopDetected = 508
        /// </summary>
        LoopDetected = 508,

        /// <summary>
        /// NotExtended = 510
        /// </summary>
        NotExtended = 510,

        /// <summary>
        /// NetworkAuthenticationRequired = 511
        /// </summary>
        NetworkAuthenticationRequired = 511,

        /// <summary>
        /// Message could not be sent to the server
        /// </summary>
        SendError = 581,
        
        /// <summary>
        /// Key or name size limit for cache key, queue name etc
        /// </summary>
        NameSizeLimit = 701,
        
        /// <summary>
        /// Value size limit for message length etc
        /// </summary>
        ValueSizeLimit = 702
    }
}