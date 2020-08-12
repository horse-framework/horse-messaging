namespace Twino.Protocols.TMQ
{
	/// <summary>
	/// Twino Result object for responses
	/// </summary>
	public class TwinoResult<TModel>: TwinoResult
	{
		/// <summary>
		/// Response model
		/// </summary>
		public TModel Model { get; }

		/// <summary>
		/// Response raw message
		/// </summary>
		public TmqMessage Message { get; }

		/// <summary>
		/// Creates new TwinoResult with a model
		/// </summary>
		public TwinoResult(TModel model, TmqMessage message, TwinoResultCode code): base(code)
		{
			Model = model;
			Message = message;

			if (code != TwinoResultCode.Ok)
			{
				if (message.Content != null && message.Length > 0)
					Reason = message.GetStringContent();
			}
		}

		/// <summary>
		/// Creates new TwinoResult with no model
		/// </summary>
		public TwinoResult(TwinoResultCode code, string reason): base(code)
		{
			Model = default;
			Reason = reason;
		}
	}

	/// <summary>
	/// Twino Messagign Queue Operation Result
	/// </summary>
	public class TwinoResult
	{
		/// <summary>
		/// Result code
		/// </summary>
		public TwinoResultCode Code { get; }

		/// <summary>
		/// Reason for unsuccessful results
		/// </summary>
		public string Reason { get; protected set; }

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