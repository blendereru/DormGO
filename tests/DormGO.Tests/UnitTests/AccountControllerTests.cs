using System.Security.Claims;
using System.Security.Principal;
using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.UnitTests;

public class AccountControllerTests
{
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

        var loggerMock = new Mock<ILogger<AccountController>>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            emailSenderMock.Object,
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            Mock.Of<IInputSanitizer>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
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
    public async Task Register_WithInvalidInput_ReturnsBadRequestResultWithValidationProblemDetails()
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
            .ReturnsAsync(IdentityResult.Failed(new IdentityError {Code = "Password", Description = "Invalid password."}));
    
        var emailSenderMock = new Mock<IEmailSender<ApplicationUser>>();
        var loggerMock = new Mock<ILogger<AccountController>>();

        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            emailSenderMock.Object,
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            Mock.Of<IInputSanitizer>());
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        
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

        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var loggerMock = new Mock<ILogger<AccountController>>();
        var currentUser = new ApplicationUser
        {
            Email = loginRequest.Email,
            UserName = loginRequest.Name,
            EmailConfirmed = true,
            Fingerprint = loginRequest.VisitorId
        };
        db.Users.Add(currentUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                    loginRequest.Password))
            .ReturnsAsync(true);
        var controller = new AccountController(
            userManagerMock.Object,
            db,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
        // Act
        var result = await controller.Login(loginRequest);

        // Assert
        var okObjectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RefreshTokensResponse>(okObjectResult.Value);
        Assert.Equal("bearer_token", response.AccessToken);
        Assert.Equal("refresh_token", response.RefreshToken);
        
        var sessionCount = await db.RefreshSessions.CountAsync(TestContext.Current.CancellationToken);
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
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var loggerMock = new Mock<ILogger<AccountController>>();
        db.Users.Add(actualUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                invalidLoginRequest.Password))
            .ReturnsAsync(false);
        var controller = new AccountController(
            userManagerMock.Object,
            db,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var loggerMock = new Mock<ILogger<AccountController>>();
        db.Users.Add(actualUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(),
                invalidLoginRequest.Password))
            .ReturnsAsync(false);
        var controller = new AccountController(
            userManagerMock.Object,
            db,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var emailSenderMock = new Mock<IEmailSender<ApplicationUser>>();
        var loggerMock = new Mock<ILogger<AccountController>>();
        var request = new PasswordForgotRequest() { Email = "your@example.com" };
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            emailSenderMock.Object,
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            Mock.Of<IInputSanitizer>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Url = Mock.Of<IUrlHelper>(u =>
            u.Action(It.IsAny<UrlActionContext>()) == "http://test/confirm");
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
    public async Task UpdateEmail_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string userId, string newEmail, string token)
    {
        // Arrange
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()));
        userManagerMock.Setup(x => x.ChangeEmailAsync(It.IsAny<ApplicationUser>(),
            It.IsAny<string>(), It.IsAny<string>()));
        var loggerMock = new Mock<ILogger<AccountController>>();
        var userHubNotificationServiceMock = new Mock<IUserHubNotificationService>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            userHubNotificationServiceMock.Object,
            loggerMock.Object,
            inputSanitizerMock.Object);

        // Act
        var result = await controller.UpdateEmail(userId, newEmail, token);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.NotNull(problemDetails.Title);
        Assert.Equal("Invalid or expired link", problemDetails.Title);
    }
    
    [Fact]
    public async Task UpdateEmail_WithValidCredentials_ReturnsNoContentResult()
    {
        // Arrange
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser());
        userManagerMock.Setup(x => x.ChangeEmailAsync(It.IsAny<ApplicationUser>(),
            It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        var loggerMock = new Mock<ILogger<AccountController>>();
        var userHubNotificationServiceMock = new Mock<IUserHubNotificationService>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            userHubNotificationServiceMock.Object,
            loggerMock.Object,
            inputSanitizerMock.Object);
        var userId = "sample_user_id";
        var newEmail = "your@example.com";
        var token = "token";
        
        // Act
        var result = await controller.UpdateEmail(userId, newEmail, token);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Theory]
    [InlineData("sample_user_id", null)]
    [InlineData(null, "token")]
    [InlineData(null, null)]
    public async Task ValidatePasswordReset_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string userId, string token)
    {
        // Arrange
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()));
        var loggerMock = new Mock<ILogger<AccountController>>();
        var userHubNotificationServiceMock = new Mock<IUserHubNotificationService>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            userHubNotificationServiceMock.Object,
            loggerMock.Object,
            inputSanitizerMock.Object);
        
        // Act
        var result = await controller.ValidatePasswordReset(userId, token);
        
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
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser());  
        var loggerMock = new Mock<ILogger<AccountController>>();
        var userHubNotificationServiceMock = new Mock<IUserHubNotificationService>();   
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            userHubNotificationServiceMock.Object,
            loggerMock.Object,
            inputSanitizerMock.Object);
        var userId = "sample_user_id";
        var token = "token";
        
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
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        var logger = new Mock<ILogger<AccountController>>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            logger.Object,
            Mock.Of<IInputSanitizer>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var loggerMock = new Mock<ILogger<AccountController>>();
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        var testUser = new ApplicationUser
        {
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = "sample_visitor_id"
        };
        var testRefreshSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var request = new UserLogoutRequest()
        {
            RefreshToken = "refresh_token",
            VisitorId = "sample_visitor_id"
        };
        await db.AddRangeAsync(testUser, testRefreshSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            db,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        
        // Act
        var result = await controller.Logout(request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Theory]
    [InlineData("sample_user_id", "token", null)]
    public async Task ConfirmEmail_WithNullParameters_ReturnsBadRequestResultWithProblemDetails(string userId, string token, string visitorId)
    {
        // Arrange
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var loggerMock = new Mock<ILogger<AccountController>>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId);
        
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

        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var loggerMock = new Mock<ILogger<AccountController>>();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        
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
        var currentUser = new ApplicationUser
        {
            Id = userId,
            Email = "your@example.com",
            UserName = "blendereru",
            Fingerprint = visitorId,
            EmailConfirmed = false
        };
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(currentUser);
        userManagerMock.Setup(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError {Code = "InvalidToken",Description = "Invalid token" }));
        var loggerMock = new Mock<ILogger<AccountController>>();
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            userManagerMock.Object, 
            Mock.Of<ApplicationContext>(),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var loggerMock = new Mock<ILogger<AccountController>>();
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var userHubNotificationServiceMock = new Mock<IUserHubNotificationService>();
        tokensProviderMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()))
            .Returns("bearer_token");
        tokensProviderMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh_token");
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        var controller = new AccountController(
            userManagerMock.Object,
            db,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            userHubNotificationServiceMock.Object,
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
        // Act
        var result = await controller.ConfirmEmail(userId, token, visitorId);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Equal("good on you", okResult.Value);
        var sessionsCount = await db.RefreshSessions.CountAsync(TestContext.Current.CancellationToken);
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
        var loggerMock = new Mock<ILogger<AccountController>>();
        var emailSenderMock = new Mock<IEmailSender<ApplicationUser>>();
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            userManagerMock.Object,
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            emailSenderMock.Object,
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.Url = Mock.Of<IUrlHelper>(u =>
            u.Action(It.IsAny<UrlActionContext>()) == "http://test/confirm");
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var loggerMock = new Mock<ILogger<AccountController>>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
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

        var loggerMock = new Mock<ILogger<AccountController>>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync((ClaimsPrincipal?)null);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();

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
        var testUser = new ApplicationUser
        {
            Id = "sample_user_id",
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = request.VisitorId
        };
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        db.Users.Add(testUser);
        db.RefreshSessions.Add(testSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var loggerMock = new Mock<ILogger<AccountController>>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns("your@example.com");
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            null!,
            db,         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var testUser = new ApplicationUser
        {
            Id = "sample_user_id",
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = request.VisitorId
        };
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        db.Users.Add(testUser);
        db.RefreshSessions.Add(testSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var loggerMock = new Mock<ILogger<AccountController>>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns("your@example.com");
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            db,         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var testUser = new ApplicationUser
        {
            Id = "sample_user_id",
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = "sample_visitor_id"
        };
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        db.Users.Add(testUser);
        db.RefreshSessions.Add(testSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var loggerMock = new Mock<ILogger<AccountController>>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns("your@example.com");
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            db,         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
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
        var testUser = new ApplicationUser
        {
            Id = "sample_user_id",
            Email = "your@example.com",
            UserName = "blendereru",
            EmailConfirmed = true,
            Fingerprint = request.VisitorId
        };
        var testSession = new RefreshSession
        {
            RefreshToken = "refresh_token",
            UA = "test_ua",
            ExpiresIn = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds(),
            Fingerprint = testUser.Fingerprint,
            Ip = "127.0.0.1",
            UserId = testUser.Id,
        };
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(options);
        db.Users.Add(testUser);
        db.RefreshSessions.Add(testSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var loggerMock = new Mock<ILogger<AccountController>>();
        var tokensProviderMock = new Mock<ITokensProvider>();
        var identityMock = new Mock<IIdentity>();
        identityMock.Setup(i => i.Name).Returns("your@example.com");
        var claimsPrincipalMock = new ClaimsPrincipal(identityMock.Object);
        tokensProviderMock
            .Setup(tp => tp.GetPrincipalFromExpiredTokenAsync(request.AccessToken))
            .ReturnsAsync(claimsPrincipalMock);
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        var controller = new AccountController(
            Mock.Of<UserManager<ApplicationUser>>(),
            db,         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProviderMock.Object,
            Mock.Of<IUserHubNotificationService>(),
            loggerMock.Object,
            inputSanitizerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        
        // Act
        var result = await controller.RefreshTokens(request);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<RefreshTokensResponse>(okResult.Value);
        var refreshSession = await db.RefreshSessions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(refreshSession.RefreshToken, response.RefreshToken);
    } 
}