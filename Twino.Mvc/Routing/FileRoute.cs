using System;
using System.Net;
using Twino.Core.Http;

namespace Twino.Mvc.Routing
{
    public class FileRoute
    {
        public string VirtualPath { get; }

        public string[] PhysicalPaths { get; }

        public Func<HttpRequest, HttpStatusCode> Validation { get; }

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