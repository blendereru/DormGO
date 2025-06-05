using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class UserHubTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OnConnectedAsync_MissingUserEmail_AbortsConnection(string? testEmail)
    {
        // Arrange
        var hub = HubTestHelper.CreateUserHub(out var context, userEmail: testEmail);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_MissingIp_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateUserHub(out var context, ipAddress: null);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.True(context.Aborted);
    }
    
    [Fact]
    public async Task OnConnectedAsync_InvalidUser_AbortsConnection()
    {
        // Arrange
        var userManager = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManager.Setup(u => u.FindByEmailAsync("invalid@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var hub = HubTestHelper.CreateUserHub(out var context,
            userEmail: "invalid@example.com",
            userManager: userManager.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_ValidInputData_SavedUserConnectionToDb()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var userManager = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        db.Users.Add(testUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hub = HubTestHelper.CreateUserHub(out var context, 
            userEmail: testUser.Email,
            userManager: userManager.Object,
            db: db);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.False(context.Aborted);
        var addedRecord = await db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(addedRecord.UserId, testUser.Id);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_WithValidInputData_RemovesConnectionFromDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        var connection = new UserConnection
        {
            ConnectionId = "disconnect-conn-id",
            UserId = userId,
            Ip = "127.0.0.1",
            ConnectedAt = DateTime.UtcNow,
            Hub = "/api/userhub"
        };
        db.UserConnections.Add(connection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hub = HubTestHelper.CreateUserHub(out _, connectionId: connection.ConnectionId, db: db);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        var remaining = await db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(remaining);
    }

}