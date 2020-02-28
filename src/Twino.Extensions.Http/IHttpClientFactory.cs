using System.Net.Http;

namespace Twino.Extensions.Http
{
    /// <summary>
    /// Handles and manages HttpClient instances
    /// </summary>
    public interface IHttpClientFactory
    {
        /// <summary>
        /// Creates new HttpClient instance from application default factory
        /// </summary>
        HttpClient Create();

        /// <summary>
        /// Creates new HttpClient instance from specified factory
        /// </summary>
        HttpClient Create(string name);

        /// <summary>
        /// Releases the HttpClient instance from application default factory
        /// </summary>
        void Release(HttpClient client);

        /// <summary>
        /// Releases the HttpClient instance from specified factory
        /// </summary>
        void Release(string name, HttpClient client);
    }
}