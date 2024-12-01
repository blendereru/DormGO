using System.Security.Claims;
using IdentityApiAuth.DTOs;
using IdentityApiAuth.Hubs;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LoginForm.Tests;
//ToDo: write integration test instead of unit test
public class SignalR_Tests
{
    [Fact]
    public async Task NotifyMessage_Should_Send_Message_To_All_Clients()
    {
        // Arrange
        var mockClients = new Mock<IHubCallerClients>();
        var mockAllClients = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.All).Returns(mockAllClients.Object);

        var hub = new PostHub
        {
            Clients = mockClients.Object
        };

        var userName = "sanzar30062000@gmail.com";
        var postDto = new PostDto()
        {
            Description = "Temp description",
            CurrentPrice = 1456,
            Latitude = 43.567,
            Longitude = 76.678,
            CreatedAt = DateTime.UtcNow,
            MaxPeople = 8,
            Members = new List<MemberDto>()
        };

        // Act
        await hub.NotifyPostCreated(userName, postDto);

        // Assert
        mockAllClients.Verify(
            client => client.SendCoreAsync(
                "ReceiveMessage",
                It.Is<object[]>(args => args[0].ToString() == message),
                default
            ),
            Times.Once
        );
    }
}
