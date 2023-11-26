using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AdvancedSample.Server.Implementations.Client;

public static class TokenProvider
{
    public static string[] ValidAudience { get; set; }
    public static string[] ValidIssuers { get; set; }

    public static KeyValuePair<bool, IEnumerable<Claim>> ValidateAccessToken(this string asymetricKey, string token,string tokenType,string tokenValue)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(asymetricKey));

        TokenValidationParameters validationParameters = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            ValidIssuers = ValidIssuers,
            ValidAudiences = ValidAudience,
            IssuerSigningKeys = new SecurityKey[] { key },
        };

        JwtSecurityTokenHandler handler = new();

        try
        {
            var claimsPrincipal = handler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            if (claimsPrincipal.Claims.Any(x => x.Type == tokenType && x.Value == tokenValue))
            {
                return new KeyValuePair<bool, IEnumerable<Claim>>(true, claimsPrincipal.Claims);
            }

            throw new Exception();
        }
        catch (Exception)
        {
            return new KeyValuePair<bool, IEnumerable<Claim>>(false, null);
        }
    }

    public static string CreateAccessToken(this string asymetricKey, string issuer, string audience, IEnumerable<Claim> claims, DateTime? Expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(asymetricKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var tokenDesc = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(claims),
            Expires = Expires,
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = credentials,
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDesc);
        var authToken = tokenHandler.WriteToken(token);

        return authToken;
    }
}