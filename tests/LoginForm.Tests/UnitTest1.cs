using System.Security.Claims;
using IdentityApiAuth.Hubs;
using IdentityApiAuth.Models;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LoginForm.Tests;

public class UnitTest1
{
    [Fact]
    public async Task OnConnectedAsync_InvalidUser_AbortsConnection()
    {
        // Arrange
        var mockDbSet = new Mock<DbSet<UserConnection>>();
        var mockDbContext = new Mock<ApplicationContext>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>();
        mockDbContext.Setup(db => db.UserConnections).Returns(mockDbSet.Object);
        var hub = new PostHub(mockDbContext.Object, mockUserManager.Object);
        var mockConnection = new Mock<HubCallerContext>();
        mockConnection.Setup(c => c.ConnectionId).Returns("testConnectionId");
        mockConnection.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "nonExistentUser") })));
        hub.Context = mockConnection.Object;

        mockUserManager.Setup(m => m.FindByNameAsync("nonExistentUser")).ReturnsAsync((ApplicationUser)null);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        mockDbSet.Verify(db => db.Add(It.IsAny<UserConnection>()), Times.Never);
    }
}