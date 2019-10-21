using System.Collections.Generic;
using System.Security.Claims;
using Twino.Core.Http;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// JSON Web Token Create, Refresh, Read operations implementation
    /// </summary>
    public interface IJwtProvider
    {
        /// <summary>
        /// Creates new JSON Web Token for specified userId, UserName and claim list
        /// </summary>
        JwtToken Create(string userId, string username, IEnumerable<Claim> claims);

        /// <summary>
        /// Reads string token and creates same token with new valid lifetime.
        /// If currentToken is not valid, returns null.
        /// </summary>
        JwtToken Refresh(string currentToken);

        /// <summary>
        /// Reads token from Request and creates ClaimsPrincipals if the token is valid
        /// </summary>
        ClaimsPrincipal Read(HttpRequest request);
    }
}