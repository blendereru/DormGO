using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.Enums;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
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
        await DataSeedHelper.SeedPostDataAsync(_db, testUser, false); 
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);

        // Act
        var result = await _controller.SearchPosts(searchRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var posts = Assert.IsType<List<PostResponse>>(okResult.Value);
        const int expectedPostCount = 5;
        Assert.Equal(expectedPostCount, posts.Count);
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
    
    [Theory]
    [InlineData(MembershipType.Own)]
    [InlineData(MembershipType.Joined)]
    [InlineData(MembershipType.NotJoined)]
    public async Task ReadPosts_WhenNoPostsExist_ReturnsOkResultWithEmptyList(MembershipType membershipType)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.ReadPosts(membershipType);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var posts = Assert.IsType<List<PostResponse>>(okResult.Value);
        Assert.Empty(posts);
    }
    
    [Fact]
    public async Task ReadPosts_WhenOwnPostsExist_ReturnsOkResultWithPostResponse()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await DataSeedHelper.SeedPostDataAsync(_db, testUser, false);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.ReadPosts(MembershipType.Own);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var posts = Assert.IsType<List<PostResponse>>(okResult.Value);
        const int expectedPostCount = 5;
        Assert.Equal(expectedPostCount, posts.Count);
    }

    [Fact]
    public async Task ReadPost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.ReadPost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task ReadPost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.ReadPost(testId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task ReadPost_WhenPostExists_ReturnsOkResultWithPostResponse()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 0,
            CreatorId = testUser.Id
        };
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.ReadPost(testPost.Id);
        
        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var post = Assert.IsType<PostResponse>(okResult.Value);
        Assert.Equal(testPost.Id, post.Id);
    }

    [Fact]
    public async Task JoinPost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.JoinPost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task JoinPost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.JoinPost(testId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }
    
    [Fact] 
    public async Task JoinPost_WhenPostIsFull_ReturnsConflictResultWithProblemDetails() 
    {
        var testPostCreator = UserHelper.CreateUser();
        var testUser = UserHelper.CreateUser();
        _db.Users.AddRange(testPostCreator, testUser);
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            Members = new List<ApplicationUser>() {UserHelper.CreateUser()},
            MaxPeople = 1,
            CreatorId = testPostCreator.Id
        };
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.JoinPost(testPost.Id);
        
        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal("Post is full", problemDetails.Title);
    }
    
    [Fact]
    public async Task JoinPost_WhenPostExists_ReturnsNoContentResult()
    {
        // Arrange
        var testPostCreator = UserHelper.CreateUser();
        var testUser = UserHelper.CreateUser();
        _db.Users.AddRange(testPostCreator, testUser);
        const int testMaxPeople = 5;
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = testMaxPeople,
            CreatorId = testPostCreator.Id
        };
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.JoinPost(testPost.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
    }

    [Fact]
    public async Task TransferPostOwnership_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var request = new OwnershipTransferRequest
        {
            Email = "your@example.com"
        };
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        
        // Act
        var result = await _controller.TransferPostOwnership(testId, request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Fact]
    public async Task TransferPostOwnership_WithNotEnoughData_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var request = new OwnershipTransferRequest();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.TransferPostOwnership(testId, request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The email field is required";
        Assert.Contains(error, problemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task TransferPostOwnership_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        var request = new OwnershipTransferRequest
        {
            Email = "your@anotherexample.com"
        };
        
        // Act
        var result = await _controller.TransferPostOwnership(testId, request);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task TransferPostOwnership_WhenPostExists_ReturnsNoContentResult()
    {
        // Arrange
        var testPostCreator = UserHelper.CreateUser();
        var testUser = UserHelper.CreateUser();
        testUser.Email = "your@anotherexample.com";
        _db.Users.AddRange(testPostCreator, testUser);
        const int testMaxPeople = 5;
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = testMaxPeople,
            CreatorId = testPostCreator.Id,
            Members = new List<ApplicationUser>() {testUser}
        };
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testPostCreator);
        var request = new OwnershipTransferRequest
        {
            Email = testUser.Email
        };
        
        // Act
        var result = await _controller.TransferPostOwnership(testPost.Id, request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testUser.Id, post.CreatorId);
    }

    [Fact]
    public async Task UpdatePost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.UpdatePost(testId, new PostUpdateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Fact]
    public async Task UpdatePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdatePost(testId, new PostUpdateRequest());
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task UpdatePost_WithMembersToRemove_RemovesMembersAndReturnsNoContentResult()
    {
        // Arrange
        var testCreator = UserHelper.CreateUser();
        var testUserToRemove = UserHelper.CreateUser();
        var request = new PostUpdateRequest
        {
            Title = "New test title",
            Description = "New test description",
            MembersToRemove = new List<UserToRemoveRequest>
            {
                new UserToRemoveRequest { Id = testUserToRemove.Id }
            }
        };
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 0,
            CreatorId = testCreator.Id
        };
        _db.Users.AddRange(testCreator, testUserToRemove);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testCreator);
        
        // Act
        var result = await _controller.UpdatePost(testPost.Id, request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(testUserToRemove, post.Members);
    }
    
    [Fact]
    public async Task UpdatePost_WithValidInput_ReturnsNoContentResult()
    {
        // Arrange
        var testCreator = UserHelper.CreateUser();
        var request = new PostUpdateRequest
        {
            Title = "New test title",
            Description = "New test description",
            
        };
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 0,
            CreatorId = testCreator.Id
        };
        _db.Users.Add(testCreator);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testCreator);
        
        // Act
        var result = await _controller.UpdatePost(testPost.Id, request);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(request.Title, post.Title);
        Assert.Equal(request.Description, post.Description);
    }

    [Fact]
    public async Task LeavePost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.LeavePost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task LeavePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.LeavePost(testId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task LeavePost_WhenUserNotOwnerAndNotMember_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testCreator = UserHelper.CreateUser();
        var currentTestUser = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            CreatorId = testCreator.Id
        };
        _db.Users.AddRange(testCreator, currentTestUser);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, currentTestUser);
        
        // Act
        var result = await _controller.LeavePost(testPost.Id);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task LeavePost_WhenUserOwnerAndNoMembersLeft_RemovesPostAndReturnsNoContentResult()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            CreatorId = testUser.Id
        };
        _db.Users.Add(testUser);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.LeavePost(testPost.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(post);
    }

    [Fact]
    public async Task LeavePost_WhenUserOwner_LeavesHimselfAndMakesCreatorAnotherMemberAndReturnsNoContentResult()
    {
        // Arrange
        var testCreator = UserHelper.CreateUser();
        var currentTestUser = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            Members = new List<ApplicationUser> {currentTestUser},
            CreatorId = testCreator.Id
        };
        _db.Users.AddRange(testCreator, currentTestUser);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testCreator);
        
        // Act
        var result = await _controller.LeavePost(testPost.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Single(post.Members);
        Assert.Equal(currentTestUser.Id, post.CreatorId);
    }

    [Fact]
    public async Task LeavePost_WhenUserMember_LeavesPostAndReturnsNoContentResult()
    {
        // Arrange
        var testCreator = UserHelper.CreateUser();
        var currentTestUser = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            Members = new List<ApplicationUser> {currentTestUser},
            CreatorId = testCreator.Id
        };
        _db.Users.AddRange(testCreator, currentTestUser);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, currentTestUser);
        
        // Act
        var result = await _controller.LeavePost(testPost.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Empty(post.Members);
        Assert.Equal(testCreator.Id, post.CreatorId);
    }

    [Fact]
    public async Task DeletePost_ForUnauthorizedUser_ReturnsUnauthorizedResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _controller.DeletePost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task DeletePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString();
        var testUser = UserHelper.CreateUser();
        _db.Users.Add(testUser);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeletePost(testId);
        
        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("Not found", problemDetails.Title);
    }

    [Fact]
    public async Task DeletePost_WhenUserNotCreator_ReturnsForbiddenResultWithProblemDetails()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var testCreator = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            Members = new List<ApplicationUser> { testUser },
            CreatorId = testCreator.Id
        };
        _db.Users.AddRange(testUser, testCreator);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeletePost(testPost.Id);
        
        // Assert
        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbiddenResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(forbiddenResult.Value);
        Assert.Equal("Forbidden", problemDetails.Title);
    }

    [Fact]
    public async Task DeletePost_WhenUserCreator_RemovesPostAndReturnsNoContentResult()
    {
        // Assert
        var testUser = UserHelper.CreateUser();
        var testPost = new Post
        {
            Id = Guid.NewGuid().ToString(),
            Title = "title",
            Description = "description",
            Latitude = 0,
            Longitude = 0,
            CurrentPrice = 0,
            CreatedAt = DateTime.UtcNow,
            CreatorId = testUser.Id
        };
        _db.Users.Add(testUser);
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeletePost(testPost.Id);
        
        // Assert
        var noContentResult = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContentResult.StatusCode);
        var post = await _db.Posts.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(post);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}