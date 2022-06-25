namespace Horse.Messaging.Server
{
	/// <summary>
	/// Server default options
	/// </summary>
	public class HorseRiderOptions
	{
		/// <summary>
		/// Server name, will be used while connecting to other instances
		/// </summary>
		public string Name { get; set; } = "horse";

		/// <summary>
		/// Server type, will be used while connecting to other instances
		/// </summary>
		public string Type { get; set; } = "messaging";

		/// <summary>
		/// Data path for all riders
		/// </summary>
		public string DataPath { get; set; } = "data";

		/// <summary>
		/// Maximum queue limit of the server
		/// Zero is unlimited.
		/// </summary>
		public int QueueLimit { get; set; }

		/// <summary>
		/// Maximum client limit of the server
		/// Zero is unlimited
		/// </summary>
		public int ClientLimit { get; set; }

		/// <summary>
		/// Maximum channel limit of the server
		/// Zero is unlimited.
		/// </summary>
		public int ChannelLimit { get; set; }

		/// <summary>
		/// Maximum router limit of the server
		/// Zero is unlimited.
		/// </summary>
		public int RouterLimit { get; set; }
	}
}