using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Twino.Server
{
    /// <summary>
    /// HttpServer options.
    /// This object is loaded from "twino.json" or "server.json" file
    /// Or It can be passed as parameter to the HttpServer constructor method
    /// </summary>
    public class ServerOptions
    {
        /// <summary>
        /// Ping Interval in SECONDS to active websocket connections. Default is 60.
        /// </summary>
        public int PingInterval { get; set; }

        /// <summary>
        /// For TcpListener objects, maximum pending connections waiting for being accepted by the server.
        /// If a client behind the maximum pending connections, it will be rejected immediately.
        /// Default is 0 (means disabled)
        /// </summary>
        public int MaximumPendingConnections { get; set; }

        /// <summary>
        /// Hosts
        /// </summary>
        public List<HostOptions> Hosts { get; set; }

        /// <summary>
        /// HTTP Request timeout in SECONDS. Default is 30.
        /// </summary>
        public int RequestTimeout { get; set; }

        /// <summary>
        /// Preferred content encoding method.
        /// br and gzip are supported.
        /// </summary>
        public string ContentEncoding { get; set; }

        /// <summary>
        /// Supported options files
        /// </summary>
        private static readonly string[] OptionsFiles = { "server.json", "twino.json" };

        /// <summary>
        /// Finds the filename from supported file list.
        /// If exists, loads options from file.
        /// If not exists, returns default options.
        /// </summary>
        internal static ServerOptions LoadFromFile(string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
                foreach (string file in OptionsFiles)
                {
                    if (File.Exists(file))
                    {
                        filename = file;
                        break;
                    }
                }

            if (string.IsNullOrEmpty(filename))
                return CreateDefault();

            string serialized = File.ReadAllText(filename);
            ServerOptions options = JsonConvert.DeserializeObject<ServerOptions>(serialized);

            if (options.RequestTimeout == 0)
                options.RequestTimeout = 120;

            if (options.PingInterval == 0)
                options.PingInterval = 120;

            if (!string.IsNullOrEmpty(options.ContentEncoding))
                options.ContentEncoding = options.ContentEncoding.ToLower(new System.Globalization.CultureInfo("en-US")).Trim();

            if (options.Hosts == null)
                return options;

            foreach (HostOptions host in options.Hosts)
            {
                if (!string.IsNullOrEmpty(host.SslProtocol))
                    host.SslProtocol = host.SslProtocol.ToLower(new System.Globalization.CultureInfo("en-US"));
            }

            return options;
        }

        /// <summary>
        /// Creates default server options
        /// </summary>
        /// <returns></returns>
        public static ServerOptions CreateDefault()
        {
            return new ServerOptions
            {
                RequestTimeout = 120,
                MaximumPendingConnections = 0,
                PingInterval = 120,
                Hosts = new List<HostOptions>
                               {
                                   new HostOptions
                                   {
                                       Port = 80
                                   }
                               }
            };
        }
    }
}