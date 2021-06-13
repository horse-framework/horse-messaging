using System;
using System.Collections.Generic;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;

namespace Horse.Messaging.Client.Direct
{
	internal class DirectTypeResolver : ITypeDescriptorResolver<DirectTypeDescriptor>
	{
		/// <summary>
		/// Resolves model type descriptor
		/// </summary>
		public DirectTypeDescriptor Resolve<TModel>(DirectTypeDescriptor defaultDescriptor)
		{
			return Resolve(typeof(TModel), defaultDescriptor);
		}

		/// <summary>
		/// Resolves model type descriptor
		/// </summary>
		public DirectTypeDescriptor Resolve(Type type, DirectTypeDescriptor defaultDescriptor)
		{
			DirectTypeDescriptor descriptor = new();

			if (defaultDescriptor != null)
				ResolveDefaults(type, descriptor, defaultDescriptor);

			ResolveDescriptor(type, descriptor);
			return descriptor;
		}


		/// <summary>
		/// Resolves default values from model type configurator
		/// </summary>
		private static void ResolveDefaults(Type type, DirectTypeDescriptor descriptor, DirectTypeDescriptor defaultConfigurator)
		{
			if (defaultConfigurator.DirectTargetFactory != null)
			{
				descriptor.DirectTarget = defaultConfigurator.DirectTargetFactory(type);
				descriptor.DirectTargetSpecified = true;
			}

			foreach (Func<KeyValuePair<string, string>> func in defaultConfigurator.HeaderFactories)
				descriptor.Headers.Add(func());
		}

		private static void ResolveDescriptor(Type type, DirectTypeDescriptor descriptor)
		{
			descriptor.Type = type;

			DirectTargetAttribute targetAttribute = type.GetCustomAttribute<DirectTargetAttribute>(true);
			if (targetAttribute != null)
			{
				descriptor.DirectValue = targetAttribute.Value;
				descriptor.FindBy = targetAttribute.FindBy;
				switch (targetAttribute.FindBy)
				{
					case FindTargetBy.Id:
						descriptor.DirectTarget = targetAttribute.Value;
						break;

					case FindTargetBy.Name:
						descriptor.DirectTarget = $"@name:{targetAttribute.Value}";
						break;

					case FindTargetBy.Type:
						descriptor.DirectTarget = $"@type:{targetAttribute.Value}";
						break;
				}
			}

			DirectContentTypeAttribute directContentTypeAttribute = type.GetCustomAttribute<DirectContentTypeAttribute>(false);
			if (directContentTypeAttribute != null)
				descriptor.ContentType = directContentTypeAttribute.ContentType;

			HighPriorityMessageAttribute prioAttr = type.GetCustomAttribute<HighPriorityMessageAttribute>(true);
			if (prioAttr != null)
				descriptor.HighPriority = true;

			IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
			foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
				descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));

		}	
	}
}