using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;

namespace DormGO.Tests.IntegrationTests;

public class PostControllerTests : IClassFixture<CustomWebApplicationFactoryFixture<Program>>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactoryFixture<Program> _factory;
    private readonly HttpClient _client;
    
    public PostControllerTests(CustomWebApplicationFactoryFixture<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.InitializeAsync();
        var user = _factory.TestUser;
        var jwtToken = TokenHelper.GenerateJwt(user.Id, user.Email, user.EmailConfirmed.ToString(),
            DateTime.UtcNow.AddMinutes(30));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
    }

    [Fact]
    public async Task CreatePost_NotifiesOtherSignalRConnections()
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
        var hubConnectionUser = new ApplicationUser
        {
            UserName = "testhub@example.com",
            Email = "testhub@example.com",
            EmailConfirmed = true
        };
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(hubConnectionUser, _factory.TestUserPassword);
        Assert.Equal(IdentityResult.Success, result);
        var baseUri = _client.BaseAddress;
        var connection = await HubConnectionHelper.ConnectUserAsync(hubConnectionUser, baseUri);
        var tcs = new TaskCompletionSource<string?>();
        connection.On<PostCreatedNotification>("PostCreated", (notification) =>
        {
            tcs.TrySetResult(notification.Id);
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/posts", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var post = await response.Content.ReadFromJsonAsync<PostResponse>(options, TestContext.Current.CancellationToken);
        Assert.NotNull(post);
        var hubPostId = await Task.WhenAny(tcs.Task, Task.Delay(5000, TestContext.Current.CancellationToken)) == tcs.Task
            ? await tcs.Task
            : null;
        Assert.NotNull(hubPostId);
        Assert.Equal(post.Id, hubPostId);
    }

        
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}