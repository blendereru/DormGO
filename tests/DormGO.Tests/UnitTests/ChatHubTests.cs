using DormGO.Data;
using DormGO.Models;
using DormGO.Tests.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DormGO.Tests.UnitTests;

public class ChatHubTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task OnConnectedAsync_WhenUserIdIsNullOrWhiteSpace_AbortsConnection(string? testUserId)
    {
        // Arrange
        var hub = HubTestHelper.CreateChatHub(out var context, userId: testUserId);

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenIpAddressNull_AbortsConnection()
    {
        // Arrange
        var hub = HubTestHelper.CreateChatHub(out var context, ipAddress: null);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_ForNonExistentPost_AbortsConnection()
    {
        // Arrange
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        var hub = HubTestHelper.CreateChatHub(out var context, userManagerMock.Object);
        
        // Act
        await hub.OnConnectedAsync();
        
        // Assert
        Assert.True(context.Aborted);
    }

    [Fact]
    public async Task OnConnectedAsync_ForValidInputData_ConnectsUser()
    {
        // Arrange
        var testUser = UserHelper.CreateUser();
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                       .ReturnsAsync(testUser);
        var groupsMock = new Mock<IGroupManager>();
        groupsMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationContext(dbOptions);
        var post = PostHelper.CreatePost(testUser);
        db.Users.Add(testUser);
        db.Posts.Add(post);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hub = HubTestHelper.CreateChatHub(
            out var context,
            userManager: userManagerMock.Object,
            db: db,
            userId: testUser.Id,
            groupManager: groupsMock.Object
        );

        // Act
        await hub.OnConnectedAsync();

        // Assert
        Assert.False(context.Aborted);
        var connection = await db.UserConnections.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(testUser.Id, connection.UserId);
        Assert.Equal("127.0.0.1", connection.Ip);
        Assert.Equal("/api/chathub", connection.Hub);
        groupsMock.Verify(g => g.AddToGroupAsync(
                It.IsAny<string>(),
                post.Id,
                It.IsAny<CancellationToken>()), Times.Once);
    }
}