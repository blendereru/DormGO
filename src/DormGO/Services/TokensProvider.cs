using System.Security.Claims;
using System.Security.Cryptography;
using DormGO.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DormGO.Services;

public class TokensProvider : ITokensProvider
{
    private readonly AuthOptions _authOptions;
    private readonly ILogger<TokensProvider> _logger;
    public TokensProvider(IOptions<AuthOptions> options, ILogger<TokensProvider> logger)
    {
        _authOptions = options.Value;
        _logger = logger;
    }
    public string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new("EmailConfirmed", user.EmailConfirmed.ToString())
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_authOptions.Lifetime),
            SigningCredentials = new SigningCredentials(_authOptions.GetSymmetricSecurityKey(), 
                SecurityAlgorithms.HmacSha256),
            Issuer = _authOptions.Issuer,
            Audience = _authOptions.Audience
        };
        var jwtHandler = new JsonWebTokenHandler();
        _logger.LogDebug("Jwt(Access token) generated. UserId: {UserId}", user.Id);
        return jwtHandler.CreateToken(tokenDescriptor);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        _logger.LogDebug("Refresh token generated.");
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<ClaimsPrincipal?> GetPrincipalFromExpiredTokenAsync(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _authOptions.Audience,
            ValidateIssuer = true,
            ValidIssuer = _authOptions.Issuer,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.Name,
            IssuerSigningKey = _authOptions.GetSymmetricSecurityKey(),
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            _logger.LogDebug("Validating JWT token");
            var handler = new JsonWebTokenHandler();

            var result = await handler.ValidateTokenAsync(token, tokenValidationParameters);

            if (!result.IsValid)
            {
                _logger.LogWarning("JWT validation failed. Error: {Error}", result.Exception?.Message ?? "Unknown error");
                return null;
            }

            if (result.Claims[JwtRegisteredClaimNames.Sub] is string userId)
            {
                _logger.LogDebug("JWT token successfully validated. UserId: {UserId}", userId);
            }
            return new ClaimsPrincipal(result.ClaimsIdentity);
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception while validating JWT: {Message}", ex.Message);
            return null;
        }
    }
}