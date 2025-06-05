using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class NotificationHubTests
{
    [Fact]
    public async Task OnConnectedAsync_MissingUserId_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateNotificationHub(out var context, userId: string.Empty);
        
        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_MissingIpAddress_AbortsConnection()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.FindByIdAsync(testUser.Id)).ReturnsAsync(testUser);
        var hub = HubTestHelper.CreateNotificationHub(out var context, userManager: userManagerMock.Object, 
            userId: testUser.Id, ipAddress: null);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_UserNotFound_AbortsConnection()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser)null!);
        var hub = HubTestHelper.CreateNotificationHub(out var context, userManager: userManagerMock.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidUser_ConnectsSuccessfully()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.FindByIdAsync(testUser.Id)).ReturnsAsync(testUser);
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        db.Users.Add(testUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hub = HubTestHelper.CreateNotificationHub(out _, userManager: userManagerMock.Object, db: db, userId: testUser.Id);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        var connection = await db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(connection);
        Assert.Equal(testUser.Id, connection.UserId);
        Assert.Equal("/api/notificationhub", connection.Hub);
    }

    [Fact] 
    public async Task OnDisconnectedAsync_ConnectionNotFound_LogsWarning()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        db.Users.Add(testUser);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateNotificationHub(out _, db: db, userId: testUser.Id,
            groupManager: groupsMock.Object, connectionId: "missing-conn");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupsMock.Verify(g => g.RemoveFromGroupAsync("missing-conn",
            testUser.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithValidConnection_RemovesConnection()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var connection = new UserConnection
        {
            ConnectionId = "conn-1",
            UserId = testUser.Id,
            Ip = "127.0.0.1",
            Hub = "/api/notificationhub",
            ConnectedAt = DateTime.UtcNow
        };
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        db.Users.Add(testUser);
        db.UserConnections.Add(connection);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateNotificationHub(out _, db: db, userId: testUser.Id, groupManager: groupsMock.Object, connectionId: "conn-1");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        Assert.Empty(db.UserConnections);
        groupsMock.Verify(g => g.RemoveFromGroupAsync("conn-1", testUser.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

