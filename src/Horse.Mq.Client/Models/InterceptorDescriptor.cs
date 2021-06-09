using System;
using Horse.Mq.Client.Annotations;

namespace Horse.Mq.Client.Models
{
	internal class InterceptorDescriptor
	{
		public Type ImplementedType { get; }
		public Intercept Intercept { get; }
		public InterceptorDescriptor(Type implementedType, Intercept intercept)
		{
			ImplementedType = implementedType;
			Intercept = intercept;
		}
	}
}