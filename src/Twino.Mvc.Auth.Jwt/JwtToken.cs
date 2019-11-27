using System;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// JWT Token Data.
    /// When IJwtProvider Creates new token it returns this class for more information
    /// </summary>
    public class JwtToken
    {
        /// <summary>
        /// Token creation date
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Token expire date
        /// </summary>
        public DateTime Expires { get; set; }

        /// <summary>
        /// Token issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Token scheme (for JWT it is Bearer)
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// Token string without Scheme
        /// </summary>
        public string Token { get; set; }
    }
}
