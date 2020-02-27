using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Twino.Protocols.Http;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// JSON Web Token Provider
    /// </summary>
    public class JwtProvider : IJwtProvider
    {
        /// <summary>
        /// JSON Web Token Options from ServiceContainer
        /// </summary>
        private JwtOptions Options { get; }

        /// <summary>
        /// Creates new Jwt Provider with specified options
        /// </summary>
        public JwtProvider(JwtOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// Creates new JSON Web Token for specified userId, UserName and claim list
        /// </summary>
        public JwtToken Create(string userId, string username, IEnumerable<Claim> claims)
        {
            //creates claim list
            List<Claim> tokenClaims = new List<Claim>();

            tokenClaims.Add(new Claim(JwtRegisteredClaimNames.Jti, userId));
            tokenClaims.Add(new Claim(JwtRegisteredClaimNames.Sub, username));

            if (claims != null)
                foreach (Claim claim in claims)
                    tokenClaims.Add(claim);

            //creates token
            SigningCredentials credentials = new SigningCredentials(Options.SigningKey, SecurityAlgorithms.HmacSha256);
            DateTime expiration = DateTime.Now + Options.Lifetime;

            var token = new JwtSecurityToken(Options.Issuer, Options.Audience, tokenClaims, expires: expiration, signingCredentials: credentials);
            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return new JwtToken
            {
                Created = token.ValidFrom,
                Expires = token.ValidTo,
                Issuer = token.Issuer,
                Token = tokenString,
                Scheme = HttpHeaders.BEARER
            };
        }

        /// <summary>
        /// Reads string token and creates same token with new valid lifetime.
        /// If currentToken is not valid, returns null.
        /// </summary>
        public JwtToken Refresh(string currentToken)
        {
            JwtClaimsPrincipalValidator validator = new JwtClaimsPrincipalValidator(Options);

            SecurityToken validatedToken;
            ClaimsPrincipal principal = validator.GetToken(currentToken, out validatedToken);

            if (principal == null || validatedToken == null)
                return null;

            //creates claim list
            Claim idClaim = principal.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti);
            Claim usernameClaim = principal.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub);

            if (idClaim == null || usernameClaim == null)
                return null;

            List<Claim> claims = new List<Claim>();
            foreach (Claim claim in principal.Claims)
                if (claim.Type != JwtRegisteredClaimNames.Jti && claim.Type != JwtRegisteredClaimNames.Sub)
                    claims.Add(claim);

            JwtToken refreshToken = Create(idClaim.Value, usernameClaim.Value, claims);
            return refreshToken;
        }

        /// <summary>
        /// Reads token from Request and creates ClaimsPrincipals if the token is valid
        /// </summary>
        public ClaimsPrincipal Read(HttpRequest request)
        {
            JwtClaimsPrincipalValidator validator = new JwtClaimsPrincipalValidator(Options);
            return validator.Get(request);
        }
    }
}