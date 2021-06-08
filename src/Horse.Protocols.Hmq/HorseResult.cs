namespace Horse.Protocols.Hmq
{
	/// <summary>
	/// Horse Result object for responses
	/// </summary>
	public class HorseResult<TModel>: HorseResult
	{
		/// <summary>
		/// Response model
		/// </summary>
		public TModel Model { get; }

		/// <summary>
		/// Creates new HorseResult with a model
		/// </summary>
		public HorseResult(TModel model, HorseMessage message, HorseResultCode code): base(code)
		{
			Model = model;
			Message = message;

			if (code != HorseResultCode.Ok)
			{
				if (message?.Content != null && message.Length > 0)
					Reason = message.GetStringContent();
			}
		}

		/// <summary>
		/// Creates new HorseResult with no model
		/// </summary>
		public HorseResult(HorseResultCode code, string reason): base(code)
		{
			Model = default;
			Reason = reason;
		}
	}

	/// <summary>
	/// Horse Messaging Queue Operation Result
	/// </summary>
	public class HorseResult
	{
		/// <summary>
		/// Result code
		/// </summary>
		public HorseResultCode Code { get; }

		/// <summary>
		/// Reason for unsuccessful results
		/// </summary>
		public string Reason { get; set; }

		/// <summary>
		/// Response message
		/// </summary>
		public HorseMessage Message { get; set; }

		/// <summary>
		/// Creates new result without reason
		/// </summary>
		public HorseResult(HorseResultCode code)
		{
			Code = code;
			Reason = null;
		}

		/// <summary>
		/// Creates new result with a reason.
		/// </summary>
		public HorseResult(HorseResultCode code, string reason)
		{
			Code = code;
			Reason = reason;
		}

		/// <summary>
		/// Creates sucessful result with no reason
		/// </summary>
		public static HorseResult Ok()
		{
			return new HorseResult(HorseResultCode.Ok);
		}

		/// <summary>
		/// Creates failed result with no reason
		/// </summary>
		public static HorseResult Failed()
		{
			return new HorseResult(HorseResultCode.Failed);
		}

		/// <summary>
		/// Creates timeout failed result
		/// </summary>
		public static HorseResult Timeout()
		{
			return new HorseResult(HorseResultCode.RequestTimeout);
		}

		/// <summary>
		/// Creates failed result with reason
		/// </summary>
		public static HorseResult Failed(string reason)
		{
			return new HorseResult(HorseResultCode.Failed, reason);
		}
	}
}