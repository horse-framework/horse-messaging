using System;
using System.Collections.Generic;
using System.Threading;

namespace Horse.Messaging.Client.Internal
{
    internal class TypeDescriptorContainer<TDescriptor> where TDescriptor : new()
    {
        private readonly ITypeDescriptorResolver<TDescriptor> _resolver;
        private readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();
        private readonly Dictionary<Type, TDescriptor> _descriptors = new Dictionary<Type, TDescriptor>();

        internal TDescriptor Default { get; set; } = new TDescriptor();

        /// <summary>
        /// Creates new delivery container
        /// </summary>
        public TypeDescriptorContainer(ITypeDescriptorResolver<TDescriptor> resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// Gets type delivery descriptor for type
        /// </summary>
        public TDescriptor GetDescriptor<TModel>()
        {
            return GetDescriptor(typeof(TModel));
        }

        /// <summary>
        /// Gets type delivery descriptor for type
        /// </summary>
        public TDescriptor GetDescriptor(Type type)
        {
            bool upgraded = false;
            try
            {
                _locker.EnterUpgradeableReadLock();
                _descriptors.TryGetValue(type, out TDescriptor descriptor);
                if (descriptor != null)
                    return descriptor;

                _locker.EnterWriteLock();
                upgraded = true;
                descriptor = _resolver.Resolve(type, Default);
                _descriptors.Add(type, descriptor);

                return descriptor;
            }
            finally
            {
                if (upgraded)
                    _locker.ExitWriteLock();

                _locker.ExitUpgradeableReadLock();
            }
        }
    }
}