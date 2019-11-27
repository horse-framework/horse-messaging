using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Twino.Protocols.Http;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// After HTTP Request is read, before to process the request.
    /// If a claim principal validator is registered
    /// Get method is called to read authorization and reads Bearer Token for creation JWT ClaimsPrincipal.
    /// </summary>
    public class JwtClaimsPrincipalValidator : IClaimsPrincipalValidator
    {
        /// <summary>
        /// JSON Web Token Options from ServiceContainer
        /// </summary>
        public JwtOptions Options { get; }

        public JwtClaimsPrincipalValidator(JwtOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// Reads the Request and creates ClaimsPrincipal data if user has valid JWT token
        /// </summary>
        public ClaimsPrincipal Get(HttpRequest request)
        {
            if (!request.Headers.ContainsKey(HttpHeaders.AUTHORIZATION))
                return null;

            string schemeAndToken = request.Headers[HttpHeaders.AUTHORIZATION];
            ClaimsPrincipal principal = GetToken(schemeAndToken, out _);
            return principal;
        }

        /// <summary>
        /// Creates ClaimsPrincipal from token
        /// </summary>
        public ClaimsPrincipal GetToken(string token, out SecurityToken validatedToken)
        {
            string[] schemeAndToken = token.Split(' ');
            string scheme;
            string value;

            if (schemeAndToken.Length == 1)
            {
                value = schemeAndToken[0];
            }
            else
            {
                scheme = schemeAndToken[0];
                value = schemeAndToken[1];

                if (!string.Equals(scheme, HttpHeaders.BEARER, StringComparison.InvariantCultureIgnoreCase))
                {
                    validatedToken = null;
                    return null;
                }
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            TokenValidationParameters validationParameters = new TokenValidationParameters
                                                             {
                                                                 ValidateLifetime = Options.ValidateLifetime,
                                                                 ValidateAudience = Options.ValidateAudience,
                                                                 ValidateIssuer = Options.ValidateIssuer,
                                                                 ValidIssuer = Options.Issuer,
                                                                 ValidAudience = Options.Audience,
                                                                 IssuerSigningKey = Options.SigningKey
                                                             };

            ClaimsPrincipal principal = tokenHandler.ValidateToken(value, validationParameters, out validatedToken);
            return principal;
        }
    }
}