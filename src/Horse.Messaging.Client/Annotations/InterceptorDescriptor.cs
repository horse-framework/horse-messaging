using System;

namespace Horse.Messaging.Client.Annotations
{
	/// <summary>
	/// Descritor for interceptor attribute
	/// </summary>
	public class InterceptorDescriptor
	{
		/// <summary>
		/// Type of interceptor
		/// </summary>
		public Type InterceptorType { get; }
		
		/// <summary>
		/// Interception method
		/// </summary>
		public Intercept Intercept { get; }
		
		/// <summary>
		/// Create new InterceptorDescriptor
		/// </summary>
		/// <param name="interceptorType"></param>
		/// <param name="intercept"></param>
		public InterceptorDescriptor(Type interceptorType, Intercept intercept)
		{
			InterceptorType = interceptorType;
			Intercept = intercept;
		}
	}
}