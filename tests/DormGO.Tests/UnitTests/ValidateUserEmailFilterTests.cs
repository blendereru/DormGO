using System.Security.Claims;
using DormGO.Constants;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace DormGO.Tests.UnitTests;

public class ValidateUserEmailFilterTests
{
    private readonly HttpContext _httpContext;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ActionExecutionDelegate> _actionExecutionDelegateMock;
    private readonly ValidateUserEmailFilter _filter;
    public ValidateUserEmailFilterTests()
    {
        _httpContext = new DefaultHttpContext();
        _userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        _actionExecutionDelegateMock = new Mock<ActionExecutionDelegate>();
        _filter = new ValidateUserEmailFilter(_userManagerMock.Object, Mock.Of<ILogger<ValidateUserEmailFilter>>());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task
        OnActionExecutionAsync_WithEmailClaimNullOrEmptyOrWhiteSpace_ReturnsUnauthorizedResultWithProblemDetails(string? emailClaim)
    {
        // Arrange
        var claims = new List<Claim>();
        if (emailClaim != null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, emailClaim));
        }
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _httpContext.User = new ClaimsPrincipal(identity);
        var actionContext = new ActionContext(
            _httpContext,
            new RouteData(),
            new ControllerActionDescriptor
            {
                ControllerName = "TestController",
                ActionName = "TestAction"
            });
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>());

        // Act
        await _filter.OnActionExecutionAsync(actionExecutingContext, _actionExecutionDelegateMock.Object);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
        Assert.Equal("The email claim is missing from the token.", problemDetails.Detail);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ForNonExistentUser_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.Email, "your@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _httpContext.User = new ClaimsPrincipal(identity);
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var actionContext = new ActionContext(
            _httpContext,
            new RouteData(),
            new ControllerActionDescriptor
            {
                ControllerName = "TestController",
                ActionName = "TestAction"
            });
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>());
        
        // Act
        await _filter.OnActionExecutionAsync(actionExecutingContext, _actionExecutionDelegateMock.Object);
        
        // Assert
        var result = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("User Not Found", problemDetails.Title);
        Assert.Equal("The user associated with the provided email was not found.", problemDetails.Detail);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenUserEmailNotConfirmed_ReturnsForbiddenResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        testUser.EmailConfirmed = false;
        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.Email, testUser.Email!)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _httpContext.User = new ClaimsPrincipal(identity);
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);
        var actionContext = new ActionContext(
            _httpContext,
            new RouteData(),
            new ControllerActionDescriptor
            {
                ControllerName = "TestController",
                ActionName = "TestAction"
            });
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>());
        
        // Act
        await _filter.OnActionExecutionAsync(actionExecutingContext, _actionExecutionDelegateMock.Object);
        
        // Assert
        var result = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Email Not Confirmed", problemDetails.Title);
        Assert.Equal("Email address has not been confirmed.", problemDetails.Detail);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenEverythingValid_SetsHttpContextItems()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var claims = new List<Claim>()
        {
            new(JwtRegisteredClaimNames.Email, testUser.Email!)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _httpContext.User = new ClaimsPrincipal(identity);
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);
        var actionContext = new ActionContext(
            _httpContext,
            new RouteData(),
            new ControllerActionDescriptor
            {
                ControllerName = "TestController",
                ActionName = "TestAction"
            });
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>());
        
        // Act
        await _filter.OnActionExecutionAsync(actionExecutingContext, _actionExecutionDelegateMock.Object);
        
        // Assert
        Assert.True(_httpContext.Items.ContainsKey(HttpContextItemKeys.UserItemKey), "HttpContext.Items should contain the UserItemKey.");
        var userFromContext = _httpContext.Items[HttpContextItemKeys.UserItemKey] as ApplicationUser;
        Assert.NotNull(userFromContext);
        Assert.Equal(testUser.Id, userFromContext.Id);
        Assert.Equal(testUser.Email, userFromContext.Email);

    }
}