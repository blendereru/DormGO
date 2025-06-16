using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DormGO.Tests.IntegrationTests;

public class PostControllerTests : IClassFixture<PostWebApplicationFactory>, IAsyncLifetime
{
    private readonly PostWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private HubConnection? _connection; 
    public PostControllerTests(PostWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    public async ValueTask InitializeAsync()
    { 
        await _factory.InitializeUsersAsync();
        var user = _factory.TestUser;
        var jwtToken = TokenHelper.GenerateJwt(user!, DateTime.UtcNow.AddMinutes(30));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
        _connection = await _factory.InitializeTestHubConnectionAsync(_client.BaseAddress!);
    }
 
    [Fact]
    public async Task CreatePost_NotifiesOtherSignalrConnections()
    {
        // Arrange
        Assert.NotNull(_connection);
        var request = new PostCreateRequest
        {
            Title = "title",
            Description = "description",
            CurrentPrice = 123.45m,
            Latitude = 12.34,
            Longitude = 45.68,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 12
        };
        var tcs = new TaskCompletionSource<string?>();
        _connection.On<PostCreatedNotification>("PostCreated", (notification) =>
        {
            tcs.TrySetResult(notification.Id);
        });
        
        // Act
        var response = await _client.PostAsJsonAsync("api/posts", request, TestContext.Current.CancellationToken);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var post = await response.Content.ReadFromJsonAsync<PostResponse>(options, TestContext.Current.CancellationToken);
        Assert.NotNull(post);
        var hubPostId = await Task.WhenAny(tcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken)) == tcs.Task
            ? await tcs.Task
            : null;
        Assert.NotNull(hubPostId);
        Assert.Equal(post.Id, hubPostId);
    }

    [Fact]
    public async Task CreatePost_SavesPostToDatabase()
    {
        // Arrange
        var request = new PostCreateRequest
        {
            Title = "title",
            Description = "description",
            CurrentPrice = 123.45m,
            Latitude = 12.34,
            Longitude = 45.68,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 12
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("api/posts", request, TestContext.Current.CancellationToken);
        
        // Assert
        response.EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var addedPost = await db.Posts.FirstOrDefaultAsync(p => p.Title == request.Title, TestContext.Current.CancellationToken);
        Assert.NotNull(addedPost);
        Assert.NotNull(_factory.TestUser);
        Assert.Equal(_factory.TestUser.Id, addedPost.CreatorId);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        
        GC.SuppressFinalize(this);
    }
}