using System.Security.Claims;
using DormGO.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DormGO.Tests.Helpers;

public static class TokenHelper
{
    public static string GenerateJwt(ApplicationUser user, DateTime? expiresAt)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? "your@example.com"),
            new("EmailConfirmed", user.EmailConfirmed.ToString())
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(
                    "mysupersecret_secretsecretsecretkey!123"u8.ToArray()),
                SecurityAlgorithms.HmacSha256),
            Issuer = "MyAuthServer",
            Audience = "MyAuthClient"
        };
        return tokenHandler.CreateToken(tokenDescriptor);
    }
    
    public static string GenerateExpiredJwt(ApplicationUser user)
        => GenerateJwt(user, DateTime.UtcNow.AddMinutes(-30));
}