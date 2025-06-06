using System.Security.Claims;
using System.Security.Principal;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Services;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class AccountControllerTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    public AccountControllerTests()
    {
        _db = TestDbContextFactory.CreateDbContext();
    }
    
    [Fact]
    public async Task Register_WithValidInput_ReturnsCreatedResultWithUserResponse()
    {
        //Arrange
        var registerRequest = new UserRegisterRequest
        {
            Email = "test@example.com",
            Name = "blendereru",
            Password = "strong_password123@",
            VisitorId = "sample_visitor_id"
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var emailSenderMock = new Mock<IEmailSender<ApplicationUser>>();
        emailSenderMock.Setup(x => x.SendConfirmationLinkAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object, 
            emailSender: emailSenderMock.Object);
        controller.Url = Mock.Of<IUrlHelper>(u =>
            u.Action(It.IsAny<UrlActionContext>()) == "http://test/confirm");
        
        // Act
        var result = await controller.Register(registerRequest);
        
        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<UserResponse>(createdResult.Value);
        Assert.Equal("test@example.com", response.Email);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var registerRequest = new UserRegisterRequest
        {
            Email = "test@example.com",
            Name = "blendereru",
            Password = "weak_password",
            VisitorId = "sample_visitor_id"
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError {Code = "Password",
                Description = "Invalid password."}));
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.Register(registerRequest);
         
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.Contains("Invalid password.", problemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task Login_WithValidInput_ReturnsOkResultWithTokenResponse()
    {
        // Arrange
        var loginRequest = new UserLoginRequest
        {
            Email = "your@example.com",
            Password = "strong_password123@",
            Name = "blendereru",
            VisitorId = "sample_visitor_id"
        };
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var currentUser = new ApplicationUser
        {
            Email = loginRequest.Email,
            UserName = loginRequest.Name,
            EmailConfirmed = true,
            Fingerprint = loginRequest.VisitorId
        };
        _db.Users.Add(currentUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                    loginRequest.Password))
            .ReturnsAsync(true);
        var controller = ControllerTestHelper.CreateAccountController(_db,
            userManagerMock.Object, tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.Login(loginRequest);

        // Assert
        var okObjectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RefreshTokensResponse>(okObjectResult.Value);
        Assert.Equal("bearer_token", response.AccessToken);
        Assert.Equal("refresh_token", response.RefreshToken);
        
        var sessionCount = await _db.RefreshSessions.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sessionCount);
    }

    [Fact]
    public async Task Login_WithNotEnoughData_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var invalidLoginRequest = new UserLoginRequest
        {
            Password = "strong_password123@",
            VisitorId = "sample_visitor_id"
        };
        var actualUser = new ApplicationUser
        {
            Email = invalidLoginRequest.Email,
            UserName = invalidLoginRequest.Name,
            EmailConfirmed = true,
            Fingerprint = invalidLoginRequest.VisitorId
        };
        _db.Users.Add(actualUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                invalidLoginRequest.Password))
            .ReturnsAsync(false);
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.Login(invalidLoginRequest);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        var errors = new List<string> {"The name field is required", "The email field is required"};
        Assert.Equal(errors, problemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task Login_WithInvalidUserCredentials_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var invalidLoginRequest = new UserLoginRequest
        {
            Email = "your@example.com",
            Name = "blendereru",
            Password = "incorrect_password",
            VisitorId = "sample_visitor_id"
        };
        var actualUser = new ApplicationUser
        {
            Email = invalidLoginRequest.Email,
            UserName = invalidLoginRequest.Name,
            EmailConfirmed = true,
            Fingerprint = invalidLoginRequest.VisitorId
        };
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        _db.Users.Add(actualUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                invalidLoginRequest.Password))
            .ReturnsAsync(false);
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.Login(invalidLoginRequest);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.NotNull(problemDetails.Title);
        Assert.Equal("Invalid credentials", problemDetails.Title);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsNoContentResult()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()));
        var request = new PasswordForgotRequest() { Email = "your@example.com" };
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        controller.Url = Mock.Of<IUrlHelper>(u =>
            u.Action(It.IsAny<UrlActionContext>()) == "http://test/confirm");
        
        // Act
        var result = await controller.ForgotPassword(request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Theory]
    [InlineData(null, "your@example.com", "token")]
    [InlineData("sample_user_id", null, "token")]
    [InlineData("sample_user_id", "your@example.com", null)]
    public async Task UpdateEmail_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string? userId, string? newEmail, string? token)
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()));
        userManagerMock.Setup(x => x.ChangeEmailAsync(It.IsAny<ApplicationUser>(),
            It.IsAny<string>(), It.IsAny<string>()));
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);

        // Act
        var result = await controller.UpdateEmail(userId!, newEmail!, token!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.NotNull(problemDetails.Title);
        Assert.Equal("Invalid or expired link", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateEmail_ForNonExistentUser_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testUserId = "test_user_id";
        const string testNewEmail = "your@example.com";
        const string testToken = "token";
        var controller = ControllerTestHelper.CreateAccountController(_db);
        
        // Act
        var result = await controller.UpdateEmail(testUserId, testNewEmail, testToken);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("User Not Found", problemDetails.Title);
    }
    
    [Fact]
    public async Task UpdateEmail_WithValidCredentials_ReturnsNoContentResult()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser());
        userManagerMock.Setup(x => x.ChangeEmailAsync(It.IsAny<ApplicationUser>(),
            It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        const string testUserId = "test_user_id";
        const string newTestEmail = "your@example.com";
        const string token = "token";
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.UpdateEmail(testUserId, newTestEmail, token);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Theory]
    [InlineData("sample_user_id", null)]
    [InlineData(null, "token")]
    [InlineData(null, null)]
    public async Task ValidatePasswordReset_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string? userId, string? token)
    {
        // Arrange
        var controller = ControllerTestHelper.CreateAccountController(_db);
        
        // Act
        var result = await controller.ValidatePasswordReset(userId!, token!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.NotNull(problemDetails.Title);
        Assert.Equal("Invalid or expired link", problemDetails.Title);
    }
    
    [Fact]
    public async Task ValidatePasswordReset_WithValidCredentials_ReturnsNoContentResult()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser());
        const string userId = "test_user_id";
        const string token = "token";
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.ValidatePasswordReset(userId, token);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);   
    }

    [Fact]
    public async Task ResetPassword_ReturnsNoContentResult()
    {
        // Arrange
        var request = new PasswordResetRequest
        {
            Email = "your@example.com",
            NewPassword = "strong_password123@",
            Token = "token"
        };
        var controller = ControllerTestHelper.CreateAccountController(_db);
        
        // Act
        var result = await controller.ResetPassword(request);

        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Fact]
    public async Task Logout_ReturnsNoContentResult()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var testRefreshSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var request = new UserLogoutRequest
        {
            RefreshToken = "refresh_token",
            VisitorId = "sample_visitor_id"
        };
        await _db.AddRangeAsync(testUser, testRefreshSession);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = ControllerTestHelper.CreateAccountController(_db, 
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object);
        
        // Act
        var result = await controller.Logout(request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Theory]
    [InlineData("sample_user_id", "token", null)]
    public async Task ConfirmEmail_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string userId, string token, string? visitorId)
    {
        // Arrange
        var controller = ControllerTestHelper.CreateAccountController(_db);
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.IsType<ProblemDetails>(badRequestResult.Value);
    }
    
    [Fact]
    public async Task ConfirmEmail_WithInvalidCredentials_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string userId = "sample_user_id";
        const string token = "token";
        const string visitorId = "sample_visitor_id";
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        Assert.IsType<ProblemDetails>(notFoundResult.Value);
    }
    
    [Fact] 
    
    public async Task ConfirmEmail_WithInvalidToken_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        const string userId = "sample_user_id";
        const string token = "invalid_token";
        const string visitorId = "sample_visitor_id";
        var currentTestUser = new ApplicationUser
        {
            Id = userId,
            Email = "your@example.com",
            UserName = "blendereru",
            Fingerprint = visitorId,
            EmailConfirmed = false
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(currentTestUser);
        userManagerMock.Setup(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken",
                Description = "Invalid token" }));
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.Contains("Invalid token", validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task ConfirmEmail_WithValidCredentials_ReturnsOkResult()
    {
        // Arrange
        const string userId = "sample_user_id";
        const string token = "token";
        const string visitorId = "sample_visitor_id";
        var currentUser = new ApplicationUser
        {
            Id = userId,
            Email = "your@example.com",
            UserName = "blendereru",
            Fingerprint = visitorId
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(currentUser);
        userManagerMock.Setup(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Equal("good on you", okResult.Value);
        var sessionsCount = await _db.RefreshSessions.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sessionsCount);
    }

    [Fact]
    public async Task ResendConfirmationEmail_ReturnsNoContentResult()
    {
        // Arrange
        var request = new EmailConfirmationResendRequest
        {
            Email = "your@example.com"
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()));
        userManagerMock.Setup(x => x.IsEmailConfirmedAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(false);
        var controller = ControllerTestHelper.CreateAccountController(_db, userManagerMock.Object);
        controller.Url = Mock.Of<IUrlHelper>(u =>
            u.Action(It.IsAny<UrlActionContext>()) == "http://test/confirm");
        
        // Act
        var result = await controller.ResendConfirmationEmail(request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Fact]
    public async Task RefreshTokens_InvalidAccessToken_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "invalid_access_token",
            RefreshToken = "refresh_token"
        };

        var controller = ControllerTestHelper.CreateAccountController(_db);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Invalid access token", problemDetails.Title);
    }

    [Fact]
    public async Task RefreshTokens_ForNullIdentityName_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token"
        };
        
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync((ClaimsPrincipal?)null);
        var controller = ControllerTestHelper.CreateAccountController(_db, 
            tokensProvider: tokensProviderMock.Object);

        // Act
        var result = await controller.RefreshTokens(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid access token", problemDetails.Title);
        tokensProviderMock.Verify(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken), Times.Once);
    }

    [Fact]
    public async Task RefreshTokens_ForInvalidSession_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "access_token",
            RefreshToken = "invalid_refresh_token",
            VisitorId = "sample_visitor_id"
        };
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        testUser.Fingerprint = request.VisitorId;
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        _db.RefreshSessions.Add(testSession);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns(testUser.Email);
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var controller = ControllerTestHelper.CreateAccountController(
            _db,
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid or expired credentials", problemDetails.Title);
        tokensProviderMock.Verify(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken), Times.Once);
    }
    
    [Fact]
    public async Task RefreshTokens_ForExpiredSession_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            VisitorId = "sample_visitor_id"
        };
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        testUser.Fingerprint = request.VisitorId;
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        _db.RefreshSessions.Add(testSession);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns(testUser.Email);
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var controller = ControllerTestHelper.CreateAccountController(_db,
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid or expired credentials", problemDetails.Title);
        tokensProviderMock.Verify(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken), Times.Once);
    }
    
    [Fact]
    public async Task RefreshTokens_ForInvalidFingerprint_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            VisitorId = "invalid_visitor_id"
        };
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        _db.RefreshSessions.Add(testSession);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns(testUser.Email);
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var controller = ControllerTestHelper.CreateAccountController(
            _db,
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials", problemDetails.Title);
        tokensProviderMock.Verify(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken), Times.Once);
    }
    
    [Fact] 
    public async Task RefreshTokens_WithValidCredentials_ReturnsOkResultWithRefreshTokensResponse()
    {
        // Arrange
        var request = new RefreshTokensRequest
        {
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            VisitorId = "sample_visitor_id"
        };
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        testUser.Fingerprint = request.VisitorId;
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        _db.RefreshSessions.Add(testSession);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns(testUser.Email);
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var controller = ControllerTestHelper.CreateAccountController(
            _db,
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object,
            tokensProvider: tokensProviderMock.Object);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<RefreshTokensResponse>(okResult.Value);
        var refreshSession = await _db.RefreshSessions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(refreshSession.RefreshToken, response.RefreshToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}