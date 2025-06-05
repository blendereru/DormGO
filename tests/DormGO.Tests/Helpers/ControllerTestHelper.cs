using DormGO.Controllers;
using DormGO.Data;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.Helpers;

public static class ControllerTestHelper
{
    private static TController CreateController<TController>(TController controller) where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext() { HttpContext = new DefaultHttpContext() };
        controller.ProblemDetailsFactory = new TestProblemDetailsFactory();
        return controller;
    }

    public static AccountController CreateAccountController(
        UserManager<ApplicationUser>? userManager = null,
        ApplicationContext? db = null,
        IEmailSender<ApplicationUser>? emailSender = null,
        ITokensProvider? tokensProvider = null,
        IInputSanitizer? sanitizer = null,
        IUserHubNotificationService? hubNotifier = null)
    {
        userManager ??= UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object;
        emailSender ??= Mock.Of<IEmailSender<ApplicationUser>>();
        tokensProvider ??= Mock.Of<ITokensProvider>();
        sanitizer ??= Mock.Of<IInputSanitizer>();
        hubNotifier ??= Mock.Of<IUserHubNotificationService>();
        if (db == null)
        {
            var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
    
            db = new ApplicationContext(dbOptions);
        }
        var controller = new AccountController(
            userManager,
            db,
            emailSender,
            tokensProvider,
            hubNotifier,
            Mock.Of<ILogger<AccountController>>(),
            sanitizer);
        return CreateController(controller);
    }

    public static PostController CreatePostController(ApplicationContext db)
    {
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        inputSanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        var postController = new PostController(
            db,
            Mock.Of<IPostHubNotificationService>(),
            Mock.Of<INotificationHubNotificationService<PostNotification>>(),
            Mock.Of<ILogger<PostController>>(),
            inputSanitizerMock.Object
            );
        return CreateController(postController);
    }

    public static ChatController CreateChatController(ApplicationContext db)
    {
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        inputSanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        var chatController = new ChatController(
            db,
            Mock.Of<IChatHubNotificationService>(),
            Mock.Of<ILogger<ChatController>>(),
            inputSanitizerMock.Object
        );
        return CreateController(chatController);
    }

    public static ProfileController CreateProfileController(UserManager<ApplicationUser> userManager)
    {
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        inputSanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        var profileController = new ProfileController(
            userManager,
            Mock.Of<IEmailSender<ApplicationUser>>(),
            inputSanitizerMock.Object,
            Mock.Of<ILogger<ProfileController>>()
        );
        return CreateController(profileController);
    }

    public static NotificationController CreateNotificationController(ApplicationContext db)
    {
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        inputSanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        var notificationController = new NotificationController(
            db,
            Mock.Of<ILogger<NotificationController>>(),
            inputSanitizerMock.Object
        );
        return CreateController(notificationController);
    }
}