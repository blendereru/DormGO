using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class UserHubTests : IAsyncDisposable
{
    private readonly ApplicationContext _db;
    public UserHubTests()
    {
        _db = TestDbContextFactory.CreateDbContext();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OnConnectedAsync_MissingUserEmail_AbortsConnection(string? testEmail)
    {
        // Arrange
        var hub = HubTestHelper.CreateUserHub(out var context, _db, userEmail: testEmail);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_MissingIp_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateUserHub(out var context, _db, ipAddress: null);
        
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
            _db,
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
        var testUser = await DataSeedHelper.SeedUserDataAsync(_db);
        var userManager = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);
        var hub = HubTestHelper.CreateUserHub(out var context, 
            _db,
            userEmail: testUser.Email,
            userManager: userManager.Object);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.False(context.Aborted);
        var addedRecord = await _db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(addedRecord.UserId, testUser.Id);
    }
    
    [Fact]
    public async Task OnDisconnectedAsync_WithValidInputData_RemovesConnectionFromDatabase()
    {
        // Arrange
        const string userId = "test_user_id";
        var connection = new UserConnection
        {
            ConnectionId = "disconnect-conn-id",
            UserId = userId,
            Ip = "127.0.0.1",
            ConnectedAt = DateTime.UtcNow,
            Hub = "/api/userhub"
        };
        _db.UserConnections.Add(connection);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hub = HubTestHelper.CreateUserHub(out _, _db, connectionId: connection.ConnectionId);

        // Act
        await hub.OnDisconnectedAsync(null);

        // Assert
        var remaining = await _db.UserConnections.SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.Null(remaining);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}