using DormGO.Controllers;
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
            null!,
            emailSenderMock.Object,
            null!,
            null!,
            loggerMock.Object,
            null!);
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
            null!,
            emailSenderMock.Object,
            null!,
            null!,
            loggerMock.Object,
            null!);
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
            null!,
            tokensProviderMock.Object,
            null!,
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
}