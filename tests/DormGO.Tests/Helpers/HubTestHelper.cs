using System.Net;
using DormGO.Data;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;

namespace DormGO.Tests.Helpers;

public static class HubTestHelper
{
    public static ChatHub CreateChatHub(
        out TestHubCallerContext testContext,
        UserManager<ApplicationUser>? userManager = null,
        string? connectionId = null,
        string? userId = "test-user-id",
        string? ipAddress = "127.0.0.1",
        IGroupManager? groupManager = null,
        ApplicationContext? db = null)
    {
        var logger = new Mock<ILogger<ChatHub>>();
        userManager ??= UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object;
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db ??= new ApplicationContext(options);
        var hub = new ChatHub(db, userManager, logger.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = string.IsNullOrWhiteSpace(ipAddress)
            ? null
            : IPAddress.Parse(ipAddress);
        testContext = new TestHubCallerContext(userId, connectionId, httpContext);
        hub.Context = testContext;
        groupManager ??= new Mock<IGroupManager>().Object;
        hub.Groups = groupManager;
        hub.Clients = new Mock<IHubCallerClients>().Object;
        return hub;
    }

    public static UserHub CreateUserHub(
        out TestHubCallerContext testContext,
        string? userEmail = "test@example.com",
        string? connectionId = "test-connection-id",
        string? ipAddress = "127.0.0.1",
        ApplicationContext? db = null,
        UserManager<ApplicationUser>? userManager = null,
        ILogger<UserHub>? logger = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        db ??= new ApplicationContext(options);

        userManager ??= UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object;
        logger ??= new Mock<ILogger<UserHub>>().Object;

        var hub = new UserHub(db, userManager, logger);
        var httpContext = new DefaultHttpContext();

        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var query = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "userEmail", userEmail }
            });
            httpContext.Request.Query = query;
        }
        httpContext.Connection.RemoteIpAddress = string.IsNullOrWhiteSpace(ipAddress)
            ? null
            : IPAddress.Parse(ipAddress);
        testContext = new TestHubCallerContext(null, connectionId, httpContext);
        hub.Context = testContext;
        hub.Clients = new Mock<IHubCallerClients>().Object;
        return hub;
    }
    
    public static PostHub CreatePostHub(
        out TestHubCallerContext context,
        UserManager<ApplicationUser>? userManager = null,
        string? userId = "test-user-id",
        string? connectionId = null,
        string? ipAddress = "127.0.0.1",
        ApplicationContext? db = null,
        IGroupManager? groupManager = null)
    {
        var logger = new Mock<ILogger<PostHub>>();
        userManager ??= UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object;

        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db ??= new ApplicationContext(options);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : IPAddress.Parse(ipAddress);
        context = new TestHubCallerContext(userId, connectionId, httpContext);
        var hub = new PostHub(db, userManager, logger.Object)
        {
            Context = context,
            Groups = groupManager ?? new Mock<IGroupManager>().Object,
            Clients = new Mock<IHubCallerClients>().Object
        };
        return hub;
    }
    
    public static NotificationHub CreateNotificationHub(
        out TestHubCallerContext context,
        UserManager<ApplicationUser>? userManager = null,
        string? userId = "test-user-id",
        string? connectionId = null,
        string? ipAddress = "127.0.0.1",
        ApplicationContext? db = null,
        IGroupManager? groupManager = null)
    {
        var logger = new Mock<ILogger<NotificationHub>>();
        userManager ??= UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object;
        var options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db ??= new ApplicationContext(options);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = string.IsNullOrWhiteSpace(ipAddress)
            ? null
            : IPAddress.Parse(ipAddress);
        context = new TestHubCallerContext(userId, connectionId, httpContext);
        var hub = new NotificationHub(db, userManager, logger.Object)
        {
            Context = context,
            Groups = groupManager ?? new Mock<IGroupManager>().Object,
            Clients = new Mock<IHubCallerClients>().Object
        };
        return hub;
    }
}
