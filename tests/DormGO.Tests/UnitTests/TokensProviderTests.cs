using System.Security.Claims;
using DormGO.Constants;
using DormGO.Models;
using DormGO.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace DormGO.Tests.UnitTests;

public class TokensProviderTests
{
    private readonly TokensProvider _sut;
    public TokensProviderTests()
    {
        var logger = new Mock<ILogger<TokensProvider>>();
        _sut = new TokensProvider(logger.Object);
    }
    
    [Fact]
    public void GenerateAccessToken_WithValidUser_ReturnsTokenContainingExpectedClaims()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "sanzar30062000@gmail.com",
            UserName = "blendereru",
            EmailConfirmed = false
        };
        
        var token = _sut.GenerateAccessToken(user);
        var jwtHandler = new JsonWebTokenHandler();
        var jwt = jwtHandler.ReadJsonWebToken(token);
        var claims = jwt.Claims.ToDictionary(c => c.Type, c => c.Value);
        
        Assert.NotNull(token);
        Assert.True(jwtHandler.CanReadToken(token));
        Assert.NotNull(jwt);
        Assert.NotEmpty(claims);
        
        Assert.Equal(user.Id, claims["sub"]);
        Assert.Equal(user.Email, claims["email"]);
        if (claims.TryGetValue("EmailConfirmed", out var emailVerified))
        {
            Assert.Equal(user.EmailConfirmed.ToString().ToLower(), emailVerified.ToLower());
        }
        
        Assert.True(claims.ContainsKey("iat"), "Missing 'iat' (issued-at) claim.");
        Assert.True(claims.ContainsKey("exp"), "Missing 'exp' (expiration) claim.");
    }
    [Fact]
    public async Task GetPrincipalFromExpiredToken_WithExpiredToken_ReturnsPrincipal()
    {
        var userId = Guid.NewGuid().ToString();
        var email = "sanzar30062000@gmail.com";
        var emailConfirmed = Boolean.TrueString;
        var expiredJwt = GenerateExpiredJwt(userId, email, emailConfirmed);
        
        var principal = await _sut.GetPrincipalFromExpiredTokenAsync(expiredJwt);
        
        Assert.NotNull(principal);
        Assert.Equal(userId, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(emailConfirmed, principal.FindFirst("EmailConfirmed")?.Value);
    }
    
    [Fact]
    public async Task GetPrincipalFromExpiredToken_WithInvalidToken_ReturnsNull()
    {
        var invalidJwt = "this.is.not.a.valid.jwt";
        
        var principal = await _sut.GetPrincipalFromExpiredTokenAsync(invalidJwt);
        
        Assert.Null(principal);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64StringThatDecodesTo32Bytes()
    {
        var token = _sut.GenerateRefreshToken();
        
        Assert.False(string.IsNullOrWhiteSpace(token));
        var bytes = Convert.FromBase64String(token);
        Assert.Equal(32, bytes.Length);
    }
    
    [Fact]
    public void GenerateRefreshToken_WithEachCall_ReturnsDifferentValue()
    {
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();
        
        Assert.NotEqual(token1, token2);
    }
    private static string GenerateExpiredJwt(string userId, string email, string emailConfirmed)
    {
        var tokenHandler = new JsonWebTokenHandler();
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new("EmailConfirmed", emailConfirmed)
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(-30),
            SigningCredentials = new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256),
            Issuer = AuthOptions.ISSUER,
            Audience = AuthOptions.AUDIENCE
        };
        return tokenHandler.CreateToken(tokenDescriptor);
    }
}