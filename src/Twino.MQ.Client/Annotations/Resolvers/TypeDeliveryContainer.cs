using System;
using System.Collections.Generic;
using System.Threading;

namespace Twino.MQ.Client.Annotations.Resolvers
{
	/// <summary>
	/// Resolves and keeps delivery descriptors of model types
	/// </summary>
	public class TypeDeliveryContainer : ITypeDeliveryContainer
	{
		private readonly ITypeDeliveryResolver _resolver;
		private readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();

		private readonly Dictionary<Type, TypeDeliveryDescriptor> _deliveryDescriptors;

		/// <summary>
		/// Creates new delivery container
		/// </summary>
		public TypeDeliveryContainer(ITypeDeliveryResolver resolver)
		{
			_resolver = resolver;
			_deliveryDescriptors = new Dictionary<Type, TypeDeliveryDescriptor>();
		}

		/// <summary>
		/// Gets type delivery descriptor for type
		/// </summary>
		public TypeDeliveryDescriptor GetDescriptor<TModel>()
		{
			return GetDescriptor(typeof(TModel));
		}

		/// <summary>
		/// Gets type delivery descriptor for type
		/// </summary>
		public TypeDeliveryDescriptor GetDescriptor(Type type)
		{
			bool upgraded = false;
			try
			{
				_locker.EnterUpgradeableReadLock();
				_deliveryDescriptors.TryGetValue(type, out TypeDeliveryDescriptor descriptor);
				if (descriptor != null)
					return descriptor;

				_locker.EnterWriteLock();
				upgraded = true;
				descriptor = _resolver.Resolve(type);
				_deliveryDescriptors.Add(type, descriptor);

				return descriptor;
			}
			finally
			{
				if (upgraded) _locker.ExitWriteLock();
				_locker.ExitUpgradeableReadLock();
			}
		}
	}
}