using System.Security.Claims;
using Twino.Protocols.Http;

namespace Twino.Mvc.Auth
{
    /// <summary>
    /// After HTTP Request is read, before to process the request.
    /// If a claim principal validator is registered
    /// Get method is called to read authorization (or another) header data and create claims principal if the token is valid.
    /// </summary>
    public interface IClaimsPrincipalValidator
    {
        /// <summary>
        /// Reads the Request and creates ClaimsPrincipal data if user has valid token or another authentication info
        /// </summary>
        ClaimsPrincipal Get(HttpRequest request);
    }
}
