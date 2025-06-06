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

public class NotificationControllerTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly SqliteConnection _connection;
    private readonly NotificationController _controller;
    public NotificationControllerTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateSqliteDbContext();
        _controller = ControllerTestHelper.CreateNotificationController(_db);
    }

    [Fact]
    public async Task GetAllNotifications_ForUnauthorizedUser_ReturnUnauthorizedResultWithProblemDetails()
    {
        // Act
        var result = await _controller.GetAllNotifications();
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task GetAllNotifications_ForAuthorizedUser_ReturnsOkResultWithNotificationsResponse()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetAllNotifications();
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var notifications = okResult.Value as IEnumerable<NotificationResponse>;
        Assert.NotNull(notifications);
        foreach (var testNotification in notifications)
        {
            Assert.IsType<NotificationResponse>(testNotification, exactMatch: false);
        }
    }

    [Fact]
    public async Task GetAllNotifications_WithSeededNotifications_ReturnsOkResultWithNotificationsResponse()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        await DataSeedHelper.SeedPostNotificationData(_db, testUser, testPost, maxCount: 5);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.GetAllNotifications();
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var notifications = (okResult.Value as IEnumerable<NotificationResponse>)?.ToList();
        Assert.NotNull(notifications);
        const int expectedTestNotificationsCount = 5;
        Assert.Equal(expectedTestNotificationsCount, notifications.Count);
        foreach (var testNotification in notifications)
        {
            Assert.IsType<NotificationResponse>(testNotification, exactMatch: false);
        }
    }

    [Fact]
    public async Task UpdateNotification_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testNotificationId = "test_notification_id";
        
        // Act
        var result = await _controller.UpdateNotification(testNotificationId, new NotificationUpdateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdateNotification_ForNullOrEmptyId_ReturnBadRequestResultWithValidationProblemDetails(string? testNotificationId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateNotification(testNotificationId!, new NotificationUpdateRequest());
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task UpdateNotification_ForNonExistentPost_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        const string testNotificationId = "test_notification_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateNotification(testNotificationId, new NotificationUpdateRequest());
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task
        UpdateNotification_WhenNotificationMarkingAsReadRequestedWithTheValueAsPrevious_ReturnsBadRequestResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testPostNotification = await DataSeedHelper.SeedPostNotificationData(_db, testUser, testPost);
        var request = new NotificationUpdateRequest
        {
            IsRead = false
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateNotification(testPostNotification.Id, request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("No Update Performed", problemDetails.Title);
    }
    
    [Fact]
    public async Task UpdateNotification_WithValidInputData_ReturnsNoContentResult()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testPostNotification = await DataSeedHelper.SeedPostNotificationData(_db, testUser, testPost);
        var request = new NotificationUpdateRequest
        {
            IsRead = true
        };
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdateNotification(testPostNotification.Id, request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var updatedPostNotification = await _db.PostNotifications.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testPostNotification.IsRead, updatedPostNotification.IsRead);
    }
    
    [Fact]
    public async Task DeleteNotification_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        const string testNotificationId = "test_notification_id";
        
        // Act
        var result = await _controller.DeleteNotification(testNotificationId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeleteNotification_ForNullOrEmptyId_ReturnBadRequestResultWithValidationProblemDetails(string? testNotificationId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteNotification(testNotificationId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required.";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task DeleteNotification_ForNonExistentPost_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        const string testNotificationId = "test_notification_id";
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteNotification(testNotificationId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact]
    public async Task DeleteNotification_WithValidInputData_ReturnsNoContentResult()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var testPostNotification = await DataSeedHelper.SeedPostNotificationData(_db, testUser, testPost);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeleteNotification(testPostNotification.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var deletedPostNotification = await _db.PostNotifications.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(deletedPostNotification);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}