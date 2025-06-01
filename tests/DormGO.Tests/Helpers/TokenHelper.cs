using System.Security.Claims;
using DormGO.Constants;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DormGO.Tests.Helpers;

public class TokenHelper
{
    public static string GenerateJwt(string? userId, string? email, string? emailConfirmed, DateTime? expiresAt)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId ?? "sample_user_id"),
            new(JwtRegisteredClaimNames.Email, email ?? "your@example.com"),
            new("EmailConfirmed", emailConfirmed ?? Boolean.TrueString.ToLower())
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256),
            Issuer = AuthOptions.ISSUER,
            Audience = AuthOptions.AUDIENCE
        };
        return tokenHandler.CreateToken(tokenDescriptor);
    }
    
    public static string GenerateExpiredJwt(string? userId, string? email, string? emailConfirmed)
        => GenerateJwt(userId, email, emailConfirmed, DateTime.UtcNow.AddMinutes(-30));
}