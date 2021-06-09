using System;

namespace Horse.Mq.Client.Annotations
{
	/// <summary>
	/// Used for handlers (IQueueConsumer, IDirectConsumer, IRequestHandler) interception.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class HorseMessageInterceptorAttribute: Attribute
	{
		/// <summary>
		/// Type of interceptor
		/// </summary>
		public Type ImplementedType { get; set; }
		/// <summary>
		/// Interception method
		/// </summary>
		public Intercept Intercept { get; set; }

		/// <summary>
		/// Creates new HorseMessageInterceptorAttribute
		/// </summary>
		/// <param name="implementedType">Interceptor type</param>
		/// <param name="intercept">Interception method</param>
		public HorseMessageInterceptorAttribute(Type implementedType, Intercept intercept = Intercept.Before)
		{
			ImplementedType = implementedType;
			Intercept = intercept;
		}
	}
}