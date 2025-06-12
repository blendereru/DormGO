using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DormGO.DTOs.RequestDTO;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;

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
    public async Task CreatePost_WithMissingTitle_ReturnsBadRequestWithValidationProblemDetails()
    {
        // Arrange
        var requestBody = new PostCreateRequest
        {
            Description = "description",
            CurrentPrice = 123.45m,
            Latitude = 45.68,
            Longitude = 123.45,
            MaxPeople = 12,
            CreatedAt = DateTime.UtcNow
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("api/posts", requestBody, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(options, TestContext.Current.CancellationToken);
        Assert.NotNull(problemDetails);
        Assert.Contains("Title", problemDetails.Errors.Keys);
        Assert.Contains(problemDetails.Errors["Title"], message => message.Contains("The Title field is required.", StringComparison.OrdinalIgnoreCase));
    }
    
    
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}