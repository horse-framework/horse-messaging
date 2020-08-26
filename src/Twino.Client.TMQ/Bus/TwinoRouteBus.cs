using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
	/// <summary>
	/// Implementation for route messages and requests
	/// </summary>
	public class TwinoRouteBus: ITwinoRouteBus
	{
		private readonly TmqStickyConnector _connector;

		/// <summary>
		/// Creates new twino route bus
		/// </summary>
		public TwinoRouteBus(TmqStickyConnector connector)
		{
			_connector = connector;
		}

		/// <inheritdoc />
		public TmqClient GetClient()
		{
			return _connector.GetClient();
		}

		#region Publish

		/// <inheritdoc />
		public Task<TwinoResult> Publish(string routerName,
			string content,
			bool waitAcknowledge = false,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

			return client.Routers.Publish(routerName, content, waitAcknowledge, 0, messageHeaders);
		}

		/// <inheritdoc />
		public Task<TwinoResult> Publish(string routerName,
			MemoryStream content,
			bool waitAcknowledge = false,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

			return client.Routers.Publish(routerName, content.ToArray(), waitAcknowledge, 0, messageHeaders);
		}

		#endregion

		#region Json

		/// <inheritdoc />
		public Task<TwinoResult> PublishJson(object jsonObject,
			bool waitAcknowledge = false,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

			return client.Routers.PublishJson(jsonObject, waitAcknowledge, messageHeaders);
		}

		/// <inheritdoc />
		public Task<TwinoResult> PublishJson(string routerName,
			object jsonObject,
			bool waitAcknowledge = false,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			return PublishJson(routerName, jsonObject, null, waitAcknowledge, messageHeaders);
		}

		/// <inheritdoc />
		public Task<TwinoResult> PublishJson(string routerName,
			object jsonObject,
			ushort? contentType = null,
			bool waitAcknowledge = false,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

			return client.Routers.PublishJson(routerName, jsonObject, waitAcknowledge, contentType, messageHeaders);
		}

		#endregion

		#region Request

		/// <inheritdoc />
		public Task<TwinoMessage> PublishRequest(string routerName,
			string message,
			ushort contentType = 0,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult<TwinoMessage>(null);

			return client.Routers.PublishRequest(routerName, message, contentType, messageHeaders);
		}

		/// <inheritdoc />
		public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult<TResponse>(default, null, TwinoResultCode.SendError));

			return client.Routers.PublishRequestJson<TRequest, TResponse>(request, messageHeaders);
		}

		/// <inheritdoc />
		public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName,
			TRequest request,
			ushort? contentType = null,
			IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
		{
			TmqClient client = _connector.GetClient();
			if (client == null)
				return Task.FromResult(new TwinoResult<TResponse>(default, null, TwinoResultCode.SendError));

			return client.Routers.PublishRequestJson<TRequest, TResponse>(routerName, request, contentType, messageHeaders);
		}

		#endregion
	}
}