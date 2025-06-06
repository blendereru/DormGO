using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.Enums;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Tests.UnitTests;

public class PostControllerTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly SqliteConnection _connection;
    private readonly PostController _controller;
    public PostControllerTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateSqliteDbContext();
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var postRequest = new PostCreateRequest
        {
            Title = "title",
            Description = "description",
            Latitude = 12,
            Longitude = 1234,
            CurrentPrice = 12345.678m,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 5
        };
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var searchRequest = new PostSearchRequest { SearchText = text };
        await DataSeedHelper.SeedPostDataAsync(_db, testUser, 5); 
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        await DataSeedHelper.SeedPostDataAsync(_db, testUser, 5);
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
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.ReadPost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ReadPost_WithNullOrEmptyPostId_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.ReadPost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task ReadPost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
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
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.JoinPost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task JoinPost_WhenPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.JoinPost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task JoinPost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 3);
        var testPostCreator = users[0];
        var testUser = users[1];
        var testMember = users[2];
        var testPost = PostHelper.CreatePost(testPostCreator);
        testPost.MaxPeople = 1;
        testPost.Members.Add(testMember);
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 2);
        var testPostCreator = users[0];
        var testUser = users[1];
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testPostCreator);
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
        const string testId = "test_post_id";
        var request = new OwnershipTransferRequest
        {
            Email = "user@example.com"
        };
        
        // Act
        var result = await _controller.TransferPostOwnership(testId, request);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task TransferPostOwnership_WhenPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new OwnershipTransferRequest();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.TransferPostOwnership(testPostId!, request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task TransferPostOwnership_WithNotEnoughRequestBodyData_ReturnsBadRequestResultWithValidationProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var request = new OwnershipTransferRequest();
        var testUser = UserHelper.CreateUser();
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
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        var request = new OwnershipTransferRequest
        {
            Email = "user@anotherexample.com"
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 2);
        var testPostCreator = users[0];
        var testUser = users[1];
        var testPost = PostHelper.CreatePost(testPostCreator);
        testPost.Members.Add(testUser);
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
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.UpdatePost(testId, new PostUpdateRequest());
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal("Unauthorized", problemDetails.Title);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UpdatePost_WhenPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var request = new PostUpdateRequest();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.UpdatePost(testPostId!, request);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task UpdatePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
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
        var testCreator = await DataSeedHelper.SeedUserDataAsync(_db);
        var testUserToRemove = UserHelper.CreateUser();
        var request = new PostUpdateRequest
        {
            Title = "New test title",
            Description = "New test description",
            MembersToRemove = new List<UserToRemoveRequest>
            {
                new() { Id = testUserToRemove.Id }
            }
        };
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testCreator);
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
        var testCreator = await DataSeedHelper.SeedUserDataAsync(_db);
        var request = new PostUpdateRequest
        {
            Title = "New test title",
            Description = "New test description"
        };
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testCreator);
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
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.LeavePost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LeavePost_WhenPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.LeavePost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task LeavePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
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
        var testCreator = await DataSeedHelper.SeedUserDataAsync(_db);
        var currentTestUser = UserHelper.CreateUser();
        currentTestUser.Id = "another_test_user_id";
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testCreator);
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 2);
        var testCreator = users[0];
        var currentTestUser = users[1];
        var testPost = PostHelper.CreatePost(testCreator);
        testPost.Members.Add(currentTestUser);
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 2);
        var testCreator = users[0];
        var currentTestUser = users[1];
        var testPost = PostHelper.CreatePost(testCreator);
        testPost.Members.Add(currentTestUser);
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
        const string testId = "test_post_id";
        
        // Act
        var result = await _controller.DeletePost(testId);
        
        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeletePost_WhenPostIdNullOrEmpty_ReturnsBadRequestResultWithValidationProblemDetails(string? testPostId)
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, testUser);
        
        // Act
        var result = await _controller.DeletePost(testPostId!);
        
        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        var validationProblemDetails = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        const string error = "The id field is required";
        Assert.Contains(error, validationProblemDetails.Errors.SelectMany(e => e.Value));
    }
    
    [Fact]
    public async Task DeletePost_WhenPostDoesNotExist_ReturnsNotFoundResultWithProblemDetails()
    {
        // Arrange
        const string testId = "test_post_id";
        var testUser = UserHelper.CreateUser();
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
        var users = await DataSeedHelper.SeedUserDataAsync(_db, maxCount: 2);
        var testCreator = users[0];
        var currentTestUser = users[1];
        var testPost = PostHelper.CreatePost(testCreator);
        testPost.Members.Add(currentTestUser);     
        _db.Posts.Add(testPost);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        HttpContextItemsHelper.SetHttpContextItems(_controller.HttpContext, currentTestUser);
        
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
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
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}