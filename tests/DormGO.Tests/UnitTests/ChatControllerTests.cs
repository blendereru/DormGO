using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace DormGO.Tests.UnitTests;

public class ChatControllerTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly SqliteConnection _connection;
    private readonly ChatController _controller;
    public ChatControllerTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateSqliteDbContext();
        _controller = ControllerTestHelper.CreateChatController(_db);
    }
    [Fact]
    public async Task GetMessagesForPost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        
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
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessagesForPost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task GetMessagesForPost_ForNonExistentPost_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testMessageToAdd = await DataSeedHelper.SeedMessageDataAsync(_db, testUser, testPost);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessagesForPost(testPost.Id);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var message = await _db.Messages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testMessageToAdd.Id, message.Id);
    }

    [Fact]
    public async Task AddMessageToPost_WhenUserUnauthorized_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.AddMessageToPost(testId, new MessageCreateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AddMessageToPost_ForPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.AddMessageToPost(testPostId!, new MessageCreateRequest());
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task AddMessageToPost_ForNonExistentPost_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testPostId = "test_post_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.AddMessageToPost(testPostId, new MessageCreateRequest());
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task AddMessageToPost_ForValidInputData_ReturnsCreatedResultWithMessageResponse()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var request = new MessageCreateRequest
        {
            Content = "content"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.AddMessageToPost(testPost.Id, request);
        
        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        var response = Assert.IsType<MessageResponse>(createdResult.Value);
        var addedTestMessage = await _db.Messages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testPost.Id, addedTestMessage.PostId);
        Assert.Equal(testPost.Id, response.Post.Id);
    }

    [Fact]
    public async Task GetMessageById_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        
        // Act
        var result = await _controller.GetMessageById(testPostId, testMessageId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetMessageById_ForPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessageById(testPostId!, testMessageId);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetMessageById_ForMessageIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testMessageId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testPostId = "test_post_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessageById(testPostId, testMessageId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The messageId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task GetMessageById_ForNonExistentPostAndMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessageById(testPostId, testMessageId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact]
    public async Task GetMessageById_ForNonExistentMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessageById(testPost.Id, testMessageId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact]
    public async Task GetMessageById_ForValidInputData_ReturnsOkResultWithMessageResponse()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testMessage = await DataSeedHelper.SeedMessageDataAsync(_db, testUser, testPost);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetMessageById(testPost.Id, testMessage.Id);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<MessageResponse>(okResult.Value);
        var addedTestMessage = await _db.Messages.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testMessage.Id, addedTestMessage.Id);
        Assert.Equal(testMessage.PostId, addedTestMessage.PostId);
        Assert.Equal(testMessage.Id, response.Id);
        Assert.Equal(testMessage.PostId, response.Post.Id);
    }
    
    [Fact]
    public async Task UpdateMessage_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        
        // Act
        var result = await _controller.UpdateMessage(testPostId, testMessageId, new MessageUpdateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateMessage_ForPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMessage(testPostId!, testMessageId, new MessageUpdateRequest());
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateMessage_ForMessageIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testMessageId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testPostId = "test_post_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMessage(testPostId, testMessageId!, new MessageUpdateRequest());
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The messageId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdateMessage_ForNonExistentPostAndMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMessage(testPostId, testMessageId, new MessageUpdateRequest());
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact]
    public async Task UpdateMessage_ForNonExistentMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMessage(testPost.Id, testMessageId, new MessageUpdateRequest());
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateMessage_ForValidInputData_UpdatesMessageAndReturnsNoContentResult()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testMessage = await DataSeedHelper.SeedMessageDataAsync(_db, testUser, testPost);
        var request = new MessageUpdateRequest
        {
            Content = "new_test_content"
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateMessage(testPost.Id, testMessage.Id, request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var updatedTestMessage = await _db.Messages.SingleAsync(TestContext.Current.CancellationToken);
        var expectedSanitizedContent = request.Content.Trim();
        Assert.Equal(expectedSanitizedContent, updatedTestMessage.Content);
        Assert.NotNull(updatedTestMessage.UpdatedAt);
    }
    
    [Fact]
    public async Task DeleteMessage_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        
        // Act
        var result = await _controller.DeleteMessage(testPostId, testMessageId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeleteMessage_ForPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteMessage(testPostId!, testMessageId);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The postId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeleteMessage_ForMessageIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testMessageId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        const string testPostId = "test_post_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteMessage(testPostId, testMessageId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The messageId field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task DeleteMessage_ForNonExistentPostAndMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        const string testPostId = "test_post_id";
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteMessage(testPostId, testMessageId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact]
    public async Task DeleteMessage_ForNonExistentMessage_ReturnsMessageNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        const string testMessageId = "test_message_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteMessage(testPost.Id, testMessageId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task DeleteMessage_ForValidInputData_UpdatesMessageAndReturnsNoContentResult()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testMessage = await DataSeedHelper.SeedMessageDataAsync(_db, testUser, testPost);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteMessage(testPost.Id, testMessage.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var removedTestMessage = await _db.Messages.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(removedTestMessage);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}