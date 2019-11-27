using Microsoft.IdentityModel.Tokens;
using System;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// JSON Web Token Project Options
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Secure key string
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Audience
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Signing key is created from Secure key string (Key property)
        /// </summary>
        internal SymmetricSecurityKey SigningKey { get; set; }

        /// <summary>
        /// Token lifetime
        /// </summary>
        public TimeSpan Lifetime { get; set; }

        /// <summary>
        /// If true checks lifetime. Expired token is invalid
        /// </summary>
        public bool ValidateLifetime { get; set; }

        /// <summary>
        /// IF true checks audience, if they don't match, token is invalid
        /// </summary>
        public bool ValidateAudience { get; set; }

        /// <summary>
        /// IF true checks issuer, if they don't match, token is invalid
        /// </summary>
        public bool ValidateIssuer { get; set; }
    }
}
