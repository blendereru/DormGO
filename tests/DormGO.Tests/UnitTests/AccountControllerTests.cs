using DormGO.Controllers;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.UnitTests;

public class AccountControllerTests
{
    [Fact]
    public async Task Register_WithValidInput_ReturnsCreatedResultWithUserResponse()
    {
        var registerRequest = new UserRegisterRequest
        {
            Email = "test@example.com",
            Name = "blendereru",
            Password = "strong_password123@",
            VisitorId = "sample_visitor_id"
        };

        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(),
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
        
        var result = await controller.Register(registerRequest);
        
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<UserResponse>(createdResult.Value);
        Assert.Equal("test@example.com", response.Email);
    }

    [Fact]
    public async Task Register_WithInvalidInput_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        
        var registerRequest = new UserRegisterRequest
        {
            Email = "test@example.com",
            Name = "blendereru",
            Password = "weak_password",
            VisitorId = "sample_visitor_id"
        };

        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(),
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
        
        var result = await controller.Register(registerRequest);
         
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.Contains("Invalid password.", problemDetails.Errors.SelectMany(e => e.Value));
    }
    
}