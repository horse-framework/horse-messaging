using System;
using System.Collections.Generic;
using Twino.Core;
using Twino.SerializableModel.Models;
using Twino.SerializableModel.Serialization;

namespace Twino.SerializableModel
{
    /// <summary>
    /// Manages network packages that implement from JsonModel interface.
    /// Each client type must has own package reader generic type.
    /// </summary>
    public class PackageReader
    {
        private readonly Dictionary<int, PackageDescriptor> _descriptors;

        /// <summary>
        /// Model reader of package manager
        /// </summary>
        public IModelReader Reader { get; }

        /// <summary>
        /// Creates new package reader with default TwinoModelReader
        /// </summary>
        public PackageReader() : this(new TwinoModelReader())
        {
        }

        /// <summary>
        /// Creates new package reader with specified IModelReader
        /// </summary>
        public PackageReader(IModelReader reader)
        {
            Reader = reader;

            _descriptors = new Dictionary<int, PackageDescriptor>();
        }

        /// <summary>
        /// When TModel message is received to TClient clients.
        /// The parameter of this method will be called.
        /// </summary>
        public virtual void On<TModel>(Action<SocketBase, TModel> func) where TModel : class, ISerializableModel, new()
        {
            TModel sample = new TModel();
            On(sample.Type, func);
        }

        /// <summary>
        /// When data with type code is received.
        /// It will be read as TModel message.
        /// The parameter of this method will be called.
        /// </summary>
        public virtual void On<TModel>(int type, Action<SocketBase, TModel> func) where TModel : class, ISerializableModel, new()
        {
            if (_descriptors.ContainsKey(type))
                _descriptors[type].Actions.Add(func);
            else
            {
                PackageDescriptor descriptor = new PackageDescriptor
                {
                    No = type,
                    Type = typeof(TModel),
                    Actions = new List<Delegate>()
                };

                descriptor.Actions.Add(func);
                _descriptors.Add(type, descriptor);
            }
        }

        /// <summary>
        /// Reads the string message and if any method is subscribed the model event with On method, they will be called.
        /// </summary>
        public virtual void Read(SocketBase client, string message)
        {
            int type = Reader.ReadType(message);

            if (!_descriptors.ContainsKey(type))
                return;

            PackageDescriptor descriptor = _descriptors[type];

            ISerializableModel model = Reader.Read(descriptor.Type, message, true);

            if (model == null)
                return;

            foreach (var action in descriptor.Actions)
                action.DynamicInvoke(client, model);
        }
    }
}