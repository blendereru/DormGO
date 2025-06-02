using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Tests.UnitTests;

public class PostControllerTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly PostController _controller;
    public PostControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationContext(options);
        _controller = ControllerTestHelper.CreatePostController(_db);
    }
    
    [Fact]
    public async Task CreatePost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Act
        var result = await _controller.CreatePost(new PostCreateRequest());
        
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
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

        // Act
        var result = await _controller.CreatePost(postRequest);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<PostResponse>(createdResult.Value);
        var createdPost = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(response.Id, createdPost.Id);
    }

    [Fact]
    public async Task SearchPosts_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Act
        var result = await _controller.SearchPosts(new PostSearchRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("title")]
    public async Task SearchPosts_WhenSearchTextIsPresent_ReturnsOkResultWithPostResponse(string text)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var searchRequest = new PostSearchRequest { SearchText = text };
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await DataSeedHelper.SeedPostDataAsync(_db, testUser); 
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

        // Act
        var result = await _controller.SearchPosts(searchRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var posts = Assert.IsType<List<PostResponse>>(okResult.Value);
        Assert.Equal(5, posts.Count);
    }

    [Fact]
    public async Task ReadPosts_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Act
        var result = await _controller.SearchPosts(new PostSearchRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}