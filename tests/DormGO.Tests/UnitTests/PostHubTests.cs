using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class PostHubTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    public PostHubTests()
    {
        _db = TestDbContextFactory.CreateDbContext();
    }
    
    [Fact]
    public async Task OnConnectedAsync_MissingUserId_AbortsConnection()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        var hub = HubTestHelper.CreatePostHub(out var context, _db,
            userManager: userManagerMock.Object, userId: string.Empty);

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

        var hub = HubTestHelper.CreatePostHub(out var context, _db, 
            userManager: userManagerMock.Object, userId: testUser.Id, ipAddress: null);

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
        userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser)null!);

        var hub = HubTestHelper.CreatePostHub(out var context, _db, userManager: userManagerMock.Object);

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

        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreatePostHub(out var context, _db, userManager: userManagerMock.Object,
            userId: testUser.Id, groupManager: groupsMock.Object);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        var connection = await _db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(connection);
        Assert.Equal(testUser.Id, connection.UserId);
        groupsMock.Verify(g => g.AddToGroupAsync(context.ConnectionId, testUser.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_ConnectionNotFound_LogsWarning()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreatePostHub(out _, _db, userId: testUser.Id,
            groupManager: groupsMock.Object, connectionId: "conn-unknown");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupsMock.Verify(g => g.RemoveFromGroupAsync("conn-unknown", testUser.Id, It.IsAny<CancellationToken>()), Times.Once);
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
            Hub = "/api/posthub",
            ConnectedAt = DateTime.UtcNow
        };

        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        _db.UserConnections.Add(connection);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreatePostHub(out _, _db, userManager: userManagerMock.Object,
            userId: testUser.Id, groupManager: groupsMock.Object, connectionId: "conn-1");

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        Assert.Empty(_db.UserConnections);
        groupsMock.Verify(g => g.RemoveFromGroupAsync("conn-1", testUser.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}