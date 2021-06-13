using System;

namespace Horse.Messaging.Client.Annotations
{
	/// <summary>
	/// Descriptor for interceptor
	/// </summary>
	public class InterceptorTypeDescriptor
	{
		/// <summary>
		/// Interceptor type
		/// </summary>
		public Type InterceptorType { get; init; }

		/// <summary>
		/// Interceptor method
		/// </summary>
		public bool RunBefore { get; init; }

		/// <summary>
		/// Singleton instance
		/// </summary>
		internal IHorseInterceptor Instance { get; set; }

		private InterceptorTypeDescriptor() { }

		/// <summary>
		/// Interceptor type descriptor factory
		/// </summary>
		/// <returns></returns>
		public static InterceptorTypeDescriptor Create(InterceptorAttribute attr, bool createInstance)
		{
			InterceptorTypeDescriptor descriptor = new()
			{
				InterceptorType = attr.InterceptorType,
				RunBefore = attr.RunBefore,
			};
			if (createInstance)
				descriptor.Instance = (IHorseInterceptor) Activator.CreateInstance(attr.InterceptorType);
			return descriptor;
		}
	}
}