using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LoginFormApi.Tests;

public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BasicTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signup_ShouldReturnBadRequest_WhenPasswordIsInvalid()
    {
        // Arrange
        var invalidPasswordUser = new
        {
            Email = "test@kbtu.kz",
            Password = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/signup", invalidPasswordUser);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("The password isn't secure", content);
    }

    [Fact]
    public async Task Signup_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        // Arrange
        var invalidEmailUser = new
        {
            Email = "test@gmail.com",
            Password = "SecurePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/signup", invalidEmailUser);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("The email should end with `@kbtu.kz`", content);
    }
}
