using DormGO.Controllers;
using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Tests.UnitTests;

public class ChatControllerTests
{
    private readonly ApplicationContext _db;
    private readonly ChatController _controller;
    public ChatControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationContext(options);
        _controller = ControllerTestHelper.CreateChatController(_db);
    }
    [Fact]
    public async Task GetMessagesForPost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.GetMessagesForPost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetMessagesForPost_ForPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessagesForPost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId parameter is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task GetMessagesForPost_ForNonExistentPost_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessagesForPost(testId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task GetMessagesForPost_WithValidInputData_ReturnsOkResultWithMessagesResponse()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var testPost = PostHelper.CreatePost(testUser);
        var testMessageToAdd = new Message
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = testUser.Id,
            PostId = testPost.Id,
            Content = "content"
        };
        _db.Users.Add(testUser);
        _db.Posts.Add(testPost);
        _db.Messages.Add(testMessageToAdd);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessagesForPost(testPost.Id);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var message = await _db.Messages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testMessageToAdd.Id, message.Id);
    }
}