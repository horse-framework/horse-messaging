using System;
using System.Net;
using Twino.Protocols.Http;

namespace Twino.Mvc.Routing
{
    /// <summary>
    /// Route description for static files
    /// </summary>
    public class FileRoute
    {
        /// <summary>
        /// Virtual path
        /// </summary>
        public string VirtualPath { get; }

        /// <summary>
        /// Physical paths
        /// </summary>
        public string[] PhysicalPaths { get; }

        /// <summary>
        /// Physical path access validation action.
        /// If status code is not 200 OK, permission denied
        /// </summary>
        public Func<HttpRequest, HttpStatusCode> Validation { get; }

        /// <summary>
        /// Creates new file route from virtual path, physical path and validation method
        /// </summary>
        public FileRoute(string virtualPath, string[] physicalPaths, Func<HttpRequest, HttpStatusCode> validation = null)
        {
            VirtualPath = virtualPath.EndsWith('/') ? virtualPath : virtualPath + "/";
            PhysicalPaths = physicalPaths;
            Validation = validation;
            
            for (int i = 0; i < PhysicalPaths.Length; i++)
            {
                if (!PhysicalPaths[i].EndsWith('/'))
                    PhysicalPaths[i] += "/";
            }
        }
    }
}