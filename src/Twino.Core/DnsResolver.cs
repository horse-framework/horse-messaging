using System;
using System.Net;

namespace Twino.Core
{
    /// <summary>
    /// Resolves DNS informations for Http Requests.
    /// Usually used by clients (by ClientSocket or classes derived from ClientSocket)
    /// </summary>
    public class DnsResolver
    {
        /// <summary>
        /// Resolves DnsInfo class from and url
        /// </summary>
        public DnsInfo Resolve(string url)
        {
            DnsInfo info = new DnsInfo();

            //checks if protocol specified and finds end of the protocol index
            int protocol_index = url.IndexOf("//", StringComparison.Ordinal);

            //checks if path of the domain if specified
            bool hasPath = url.LastIndexOf('/') > url.IndexOf("//", StringComparison.Ordinal) + 2;

            //host search helps to find "domain.com" value of full url and removes protocol, path, port strings from the url
            //while removing this strings, they will also set to result
            //remove protocol from host search
            var host_search = protocol_index > 0 ? url.Substring(protocol_index + 2) : url;

            //if path specified set it to result and remove path from the host search
            if (hasPath)
            {
                info.Path = host_search.Substring(host_search.IndexOf('/'));
                host_search = host_search.Substring(0, host_search.IndexOf('/'));
            }
            else
                info.Path = "/";

            //now host has only domain and port info (if exists)
            int specified_port = 0;

            //if port specified, find and set it to result and remove from host search
            int port_index = host_search.IndexOf(':');
            if (port_index > 0)
            {
                specified_port = Convert.ToInt32(host_search.Split(':')[1].Trim());
                host_search = host_search.Substring(0, port_index);
            }

            //check if host is ip address or domain name
            bool byIP = host_search.Split('.').Length == 4;
            info.Hostname = host_search;

            if (byIP)
                info.IPAddress = host_search;
            else
            {
                //if the host is domain name, find the remote ip address of the domain
                IPHostEntry hostEntry = Dns.GetHostEntry(host_search);

                string ip = hostEntry.AddressList[0].ToString();
                if (ip.Length < 5 && hostEntry.AddressList.Length > 1)
                    ip = hostEntry.AddressList[1].ToString();
                info.IPAddress = ip;
            }

            //set the protocol and ssl information by protocol
            if (protocol_index > 0)
            {
                string protocol = url.Substring(0, protocol_index).ToLower().Replace(":", "");

                switch (protocol)
                {
                    case "http":
                        info.Protocol = Protocol.Http;
                        info.SSL = false;
                        break;

                    case "https":
                        info.Protocol = Protocol.Http;
                        info.SSL = true;
                        break;

                    case "ws":
                        info.Protocol = Protocol.WebSocket;
                        info.SSL = false;
                        break;

                    case "wss":
                        info.Protocol = Protocol.WebSocket;
                        info.SSL = true;
                        break;

                    case "tmq":
                        info.Protocol = Protocol.Tmq;
                        info.SSL = false;
                        break;

                    case "tmqs":
                        info.Protocol = Protocol.Tmq;
                        info.SSL = true;
                        break;
                }
            }

            //set the port if specified as specified port, otherwise find the port from protocol
            if (specified_port > 0)
                info.Port = specified_port;
            else
                info.Port = info.SSL ? 443 : 80;

            return info;
        }
    }
}