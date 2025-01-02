using IdentityApiAuth.DTOs;
using IdentityApiAuth.Hubs;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LoginForm.Tests;
public class SignalR_Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SignalR_Tests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSignalR();
            });
        });
    }

    [Fact]
    public async Task NotifyPostCreated_Should_Send_Message_To_All_Clients()
    {
        // Arrange
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5093/api/posthub", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add("Authorization", "Bearer <your_token>");
            })
            .WithAutomaticReconnect()
            .Build();
        string capturedUserName = null;
        PostDto capturedPost = null;
        hubConnection.On<string, PostDto>("PostCreated", (userName, post) =>
        {
            capturedUserName = userName;
            capturedPost = post;
        });

        await hubConnection.StartAsync();

        // Act
        var hubContext = _factory.Services.GetRequiredService<IHubContext<UserHub>>();
        var me = new MemberDto()
        {
            Email = "sanzar30062000@gmail.com",
            Name = "sanzar30062000@gmail.com"
        };
        var post = new PostDto
        {
            Description = "Test Post",
            Creator = me,
            CurrentPrice = 12345,
            Latitude = 23.475,
            Longitude = 43.567,
            MaxPeople = 4,
            Members = new List<MemberDto>(),
            CreatedAt = DateTime.UtcNow
        };
        await hubContext.Clients.All.SendAsync("PostCreated", "TestUser", post);
        await Task.Delay(100);
        // Assert
        Assert.Equal("TestUser", capturedUserName);
        Assert.NotNull(capturedPost);
        Assert.Equal("Test Post", capturedPost.Description);
        await hubConnection.StopAsync();
        await hubConnection.DisposeAsync();
    }
}

