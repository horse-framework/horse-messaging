using System;

namespace Horse.Messaging.Client.Annotations
{
	/// <summary>
	///     Used to intercept messages before or after handler
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class InterceptorAttribute : Attribute
	{
		/// <summary>
		///     Type of interceptor, it's should derived from IHorseInterceptor interface.
		/// </summary>
		public Type InterceptorType { get; }

		/// <summary>
		///     Is interceptor run before?
		/// </summary>
		public bool RunBefore { get; set; }

		/// <summary>
		///     Execution order
		/// </summary>
		public int Order { get; set; }

		/// <summary>
		///     Creates new HorseMessageInterceptorAttribute
		/// </summary>
		/// <param name="interceptorType">Interceptor type</param>
		/// <param name="order">Execution order</param>
		/// <param name="runBefore">Interception method</param>
		public InterceptorAttribute(Type interceptorType, int order = 0,  bool runBefore = true)
		{
			InterceptorType = interceptorType;
			RunBefore = runBefore;
			Order = order;
		}
	}
}