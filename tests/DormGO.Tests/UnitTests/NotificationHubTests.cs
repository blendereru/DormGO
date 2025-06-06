using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class NotificationHubTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly SqliteConnection _connection;
    public NotificationHubTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateSqliteDbContext();
    }
    
    [Fact]
    public async Task OnConnectedAsync_MissingUserId_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateNotificationHub(out var context, _db, userId: string.Empty);
        
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
        var hub = HubTestHelper.CreateNotificationHub(out var context, _db, userManager: userManagerMock.Object, 
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
        var hub = HubTestHelper.CreateNotificationHub(out var context, _db, userManager: userManagerMock.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidUser_ConnectsSuccessfully()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(m => m.FindByIdAsync(testUser.Id)).ReturnsAsync(testUser);
        var hub = HubTestHelper.CreateNotificationHub(out _, _db, 
            userManager: userManagerMock.Object, userId: testUser.Id);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        var connection = await _db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(connection);
        Assert.Equal(testUser.Id, connection.UserId);
        Assert.Equal("/api/notificationhub", connection.Hub);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ConnectionNotFound_KeepsInDb()
    {
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var groupsMock = new Mock<IGroupManager>();
        var testConnection = new UserConnection
        {
            ConnectionId = "test-conn",
            UserId = testUser.Id,
            Ip = "127.0.0.1",
            Hub = "/api/notificationhub",
            ConnectedAt = DateTime.UtcNow
        };
        _db.UserConnections.Add(testConnection);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hub = HubTestHelper.CreateNotificationHub(out _, _db, userId: testUser.Id,
            groupManager: groupsMock.Object, connectionId: "mis-conn");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupsMock.Verify(g => g.RemoveFromGroupAsync("mis-conn", testUser.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        var addedTestConnection = await _db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testConnection.ConnectionId, addedTestConnection.ConnectionId);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithValidConnection_RemovesConnection()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var connection = new UserConnection
        {
            ConnectionId = "conn-1",
            UserId = testUser.Id,
            Ip = "127.0.0.1",
            Hub = "/api/notificationhub",
            ConnectedAt = DateTime.UtcNow
        };
        _db.UserConnections.Add(connection);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateNotificationHub(out _, _db, userId: testUser.Id, groupManager: groupsMock.Object, connectionId: "conn-1");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        Assert.Empty(_db.UserConnections);
        groupsMock.Verify(g => g.RemoveFromGroupAsync("conn-1", testUser.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}

