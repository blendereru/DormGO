using DormGO.Models;
using DormGO.Services;
using DormGO.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace DormGO.Tests.UnitTests;

public class TokensProviderTests
{
    private readonly TokensProvider _sut;
    public TokensProviderTests()
    {
        var mockLogger = new Mock<ILogger<TokensProvider>>();
        var mockAuthOptions = new Mock<IOptions<AuthOptions>>();
        var testAuthOptions = new AuthOptions
        {
            Issuer = "MyAuthServer",
            Audience = "MyAuthClient",
            Key = "mysupersecret_secretsecretsecretkey!123",
            Lifetime = 30
        };
        mockAuthOptions.SetupGet(x => x.Value).Returns(testAuthOptions);
        _sut = new TokensProvider(mockAuthOptions.Object, mockLogger.Object);
    }
    
    [Fact]
    public void GenerateAccessToken_WithValidUser_ReturnsTokenContainingExpectedClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = false
        };
        
        // Act
        var token = _sut.GenerateAccessToken(user);
        var jwtHandler = new JsonWebTokenHandler();
        var jwt = jwtHandler.ReadJsonWebToken(token);
        var claims = jwt.Claims.ToDictionary(c => c.Type, c => c.Value);
        
        // Assert
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
        // Arrange
        var testUser = new ApplicationUser
        {
            Id = "sample_user_id",
            Email = "your@example.com"
        };
        var expiredJwt = TokenHelper.GenerateExpiredJwt(testUser);
        
        // Act
        var principal = await _sut.GetPrincipalFromExpiredTokenAsync(expiredJwt);
        
        // Assert
        Assert.NotNull(principal);
        Assert.Equal(testUser.Id, principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(testUser.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal(testUser.EmailConfirmed.ToString(), principal.FindFirst("EmailConfirmed")?.Value);
    }
    
    [Fact]
    public async Task GetPrincipalFromExpiredToken_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        const string invalidJwt = "this.is.not.a.valid.jwt";
        
        // Act
        var principal = await _sut.GetPrincipalFromExpiredTokenAsync(invalidJwt);
        
        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsBase64StringThatDecodesTo32Bytes()
    {
        // Act
        var token = _sut.GenerateRefreshToken();
        
        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        var bytes = Convert.FromBase64String(token);
        Assert.Equal(32, bytes.Length);
    }
    
    [Fact]
    public void GenerateRefreshToken_WithEachCall_ReturnsDifferentValue()
    {
        // Act
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();
        
        // Assert   
        Assert.NotEqual(token1, token2);
    }
}