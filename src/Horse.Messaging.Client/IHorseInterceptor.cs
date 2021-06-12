using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client
{
	/// <summary>
	///  You can intercept your handlers (IQueueConsumer, IDirectConsumer or IHorseRequestHandler) when you received message in any handler. 
	/// </summary>
	public interface IHorseInterceptor
	{
		/// <summary>
		/// Intercept recevied horse message
		/// </summary>
		/// <param name="message">HorseMessage</param>
		/// <param name="client">HorseClient</param>
		public Task Intercept(HorseMessage message, HorseClient client);
	}
}