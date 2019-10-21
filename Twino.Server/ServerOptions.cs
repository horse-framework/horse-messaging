using System.Collections.Generic;
using Newtonsoft.Json;
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
        /// Ping Interval in milliseconds to active websocket connections.
        /// Default is 60000 (60 secs)
        /// </summary>
        public int PingInterval { get; set; }

        /// <summary>
        /// After each TCP Client is accepted, the first data (this is the first HTTP Request) length.
        /// Default is 1024 KBs
        /// </summary>
        public long MaximumRequestLength { get; set; }

        /// <summary>
        /// Maximum bytes for Request Header. Default is 8KB
        /// </summary>
        public int MaximumHeaderLength { get; set; }

        /// <summary>
        /// Maximum URL size for Request. Default is 750 bytes.
        /// </summary>
        public int MaximumUriLength { get; set; }

        /// <summary>
        /// Maximum keep alive time in seconds for HTTP Requests.
        /// In this duration, server does not close the connection and waits for next HTTP requests.
        /// If you want to disable this feature, set value 0
        /// </summary>
        public int HttpConnectionTimeMax { get; set; }

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
        /// HTTP Request timeout in milliseconds. Default is 30000 (30 secs)
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
        private static readonly string[] OptionsFiles = {"server.json", "twino.json"};

        /// <summary>
        /// Finds the filename from supported file list.
        /// If exists, loads options from file.
        /// If not exists, returns default options.
        /// </summary>
        internal static ServerOptions LoadFromFile()
        {
            string filename = null;

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
                options.RequestTimeout = 30000;

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
                       HttpConnectionTimeMax = 180,
                       MaximumPendingConnections = 0,
                       MaximumRequestLength = 1024 * 1024,
                       MaximumUriLength = 750,
                       MaximumHeaderLength = 8192,
                       PingInterval = 60000,
                       RequestTimeout = 30000,
                       ContentEncoding = "gzip",
                       Hosts = new List<HostOptions>
                               {
                                   new HostOptions
                                   {
                                       Port = 80,
                                       Hostnames = null,
                                       SslEnabled = false
                                   }
                               }
                   };
        }
    }
}