using System;

namespace Horse.Messaging.Client.Annotations
{
	/// <summary>
	///     Horse message interceptor method
	/// </summary>
	public enum Intercept
	{
		/// <summary>
		///     Intercept before
		/// </summary>
		Before,
		/// <summary>
		///     Intercept after
		/// </summary>
		After
	}

	/// <summary>
	///     Used to intercept messages before or after handler
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class InterceptorAttribute: Attribute
	{
		/// <summary>
		///     Type of interceptor, it's should derived from IHorseInterceptor interface.
		/// </summary>
		public Type InterceptorType { get; }

		/// <summary>
		///     Interception method
		/// </summary>
		public Intercept Intercept { get; }

		/// <summary>
		///     Creates new HorseMessageInterceptorAttribute
		/// </summary>
		/// <param name="interceptorType">Interceptor type</param>
		/// <param name="intercept">Interception method</param>
		public InterceptorAttribute(Type interceptorType, Intercept intercept = Intercept.Before)
		{
			InterceptorType = interceptorType;
			Intercept = intercept;
		}
	}
}