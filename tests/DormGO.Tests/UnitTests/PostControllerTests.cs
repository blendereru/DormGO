using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.UnitTests;

public class PostControllerTests
{
    [Fact]
    public async Task CreatePost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PostController>>();
        var controller = new PostController(
            new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>().Options),
            Mock.Of<IPostHubNotificationService>(),
            Mock.Of<INotificationService<PostNotification, PostNotificationResponse>>(),
            loggerMock.Object,
            Mock.Of<IInputSanitizer>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        
        // Act
        var result = await controller.CreatePost(new PostCreateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task CreatePost_WithValidUser_ReturnsCreatedResult()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var postRequest = new PostCreateRequest
        {
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 0
        };
        var loggerMock = new Mock<ILogger<PostController>>();
        var postHubNotificationServiceMock = new Mock<IPostHubNotificationService>();
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationContext(options);
        db.Users.Add(testUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var controller = new PostController(
            db,
            postHubNotificationServiceMock.Object,
            Mock.Of<INotificationService<PostNotification, PostNotificationResponse>>(),
            loggerMock.Object,
            Mock.Of<IInputSanitizer>()
        );

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        HttpContextItemsHelper.SetHttpContextItems(controller.HttpContext, testUser);

        // Act
        var result = await controller.CreatePost(postRequest);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<PostResponse>(createdResult.Value);
        var createdPost = await db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(response.Id, createdPost.Id);
        postHubNotificationServiceMock.Verify(
            s => s.NotifyPostCreatedAsync(It.IsAny<ApplicationUser>(), It.IsAny<Post>()), Times.Once);
    }
}