using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class ChatHubTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    private readonly SqliteConnection _connection;
    public ChatHubTests()
    {
        (_db, _connection) = TestDbContextFactory.CreateSqliteDbContext();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OnConnectedAsync_WhenUserIdIsNullOrWhiteSpace_AbortsConnection(string? testUserId)
    {
        // Arrange
        var hub = HubTestHelper.CreateChatHub(out var context, _db, userId: testUserId);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenIpAddressNull_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateChatHub(out var context, _db, ipAddress: null);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_ForNonExistentUser_AbortsConnection()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var hub = HubTestHelper.CreateChatHub(out var context, _db, userManagerMock.Object);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_ForUserEmptyPosts_ConnectsUser()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);
        var groupsMock = new Mock<IGroupManager>();
        groupsMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hub = HubTestHelper.CreateChatHub(
            out var context,
            _db,
            userManager: userManagerMock.Object,
            userId: testUser.Id,
            groupManager: groupsMock.Object
        );
    
        // Act
        await hub.OnConnectedAsync();
    
        // Assert
        Assert.False(context.Aborted);
        var connection = await _db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testUser.Id, connection.UserId);
        Assert.Equal("127.0.0.1", connection.Ip);
        Assert.Equal("/api/chathub", connection.Hub);
        groupsMock.Verify(g => g.AddToGroupAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task OnConnectedAsync_ForValidInputData_ConnectsUser()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync(testUser);
        var groupsMock = new Mock<IGroupManager>();
        groupsMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hub = HubTestHelper.CreateChatHub(
            out var context,
            _db,
            userManager: userManagerMock.Object,
            userId: testUser.Id,
            groupManager: groupsMock.Object
        );

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.False(context.Aborted);
        var connection = await _db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testUser.Id, connection.UserId);
        Assert.Equal("127.0.0.1", connection.Ip);
        Assert.Equal("/api/chathub", connection.Hub);
        groupsMock.Verify(g => g.AddToGroupAsync(
                It.IsAny<string>(),
                testPost.Id,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ForValidUserId_RemovesUserFromGroup()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testPost = await DataSeedHelper.SeedPostDataAsync(_db, testUser);
        var groupsMock = new Mock<IGroupManager>();
        groupsMock.Setup(x => x.RemoveFromGroupAsync(It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hub = HubTestHelper.CreateChatHub(out var context,
            _db,
            userId: testUser.Id,
            groupManager: groupsMock.Object);
        
        // Act
        await hub.OnDisconnectedAsync(null);
        
        // Assert
        Assert.False(context.Aborted);
        groupsMock.Verify(g => g.RemoveFromGroupAsync(
            It.IsAny<string>(),
            testPost.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_NoUserId_DoesNotThrow()
    {
        // Arrange
        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateChatHub(out _, _db, userId: null, groupManager: groupsMock.Object);

        // Act
        var exception = await Record.ExceptionAsync(() => hub.OnDisconnectedAsync(null));

        // Assert
        Assert.Null(exception);
        groupsMock.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_UserWithNoPosts_DoesNotRemoveFromGroups()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateChatHub(out _, _db, 
            userId: testUser.Id,
            groupManager: groupsMock.Object);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        groupsMock.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), 
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_WithValidInputData_RemovesUserConnectionFromDatabase()
    {
        // Arrange
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var testUserConnection = new UserConnection
        {
            ConnectionId = "test-connection",
            UserId = testUser.Id,
            Ip = "127.0.0.1",
            ConnectedAt = DateTime.UtcNow,
            Hub = "/api/chathub"
        };
        var groupsMock = new Mock<IGroupManager>();
        var hub = HubTestHelper.CreateChatHub(out _,
            _db,
            connectionId: testUserConnection.ConnectionId,
            userId: testUser.Id,
            groupManager: groupsMock.Object);
        
        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        var removedRecord = await _db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(removedRecord);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}