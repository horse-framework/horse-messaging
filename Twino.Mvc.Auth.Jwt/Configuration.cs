using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

namespace Twino.Mvc.Auth.Jwt
{
    /// <summary>
    /// JWT Twino MVC Configuration class for extension methods
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Adds JWT Implementation to Twino MVC
        /// </summary>
        public static TwinoMvc AddJwt(this TwinoMvc twino, Action<JwtOptions> options)
        {
            JwtOptions jwtOptions = new JwtOptions();
            options(jwtOptions);

            jwtOptions.SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
            twino.ClaimsPrincipalValidator = new JwtClaimsPrincipalValidator(jwtOptions);

            twino.Services.AddSingleton<JwtOptions, JwtOptions>(jwtOptions);
            twino.Services.Add<IJwtProvider, JwtProvider>();

            return twino;
        }
    }
}