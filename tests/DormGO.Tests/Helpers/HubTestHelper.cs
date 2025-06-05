using System.Net;
using DormGO.Data;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.Helpers;

public static class HubTestHelper
{
    public static ChatHub CreateChatHub(
        out TestHubCallerContext testContext,
        UserManager<ApplicationUser>? userManager = null,
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
        testContext = new TestHubCallerContext(userId, httpContext);
        hub.Context = testContext;
        groupManager ??= new Mock<IGroupManager>().Object;
        hub.Groups = groupManager;
        hub.Clients = new Mock<IHubCallerClients>().Object;
        return hub;
    }
}
