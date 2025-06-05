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

    private static AccountController CreateAccountController(UserManager<ApplicationUser> userManager,
        ApplicationContext db, IEmailSender<ApplicationUser> emailSender, ITokensProvider provider,
        IInputSanitizer sanitizer)
    {
        var accountController = new AccountController(
            userManager,
            db,
            emailSender,
            provider,
            Mock.Of<IUserHubNotificationService>(),
            Mock.Of<ILogger<AccountController>>(),
            sanitizer);
        return CreateController(accountController);
    }

    private static AccountController CreateAccountController(UserManager<ApplicationUser> userManager,
        ApplicationContext db, IEmailSender<ApplicationUser> emailSender) 
        => CreateAccountController(userManager, db, emailSender, Mock.Of<ITokensProvider>(),
            Mock.Of<IInputSanitizer>());

    public static AccountController CreateAccountController(UserManager<ApplicationUser> userManager,
        ApplicationContext db, ITokensProvider tokensProvider)
        => CreateAccountController(userManager, db, Mock.Of<IEmailSender<ApplicationUser>>(),
            tokensProvider, Mock.Of<IInputSanitizer>());
    public static AccountController CreateAccountController(UserManager<ApplicationUser> userManager,
        ApplicationContext db)
        => CreateAccountController(userManager, db, Mock.Of<IEmailSender<ApplicationUser>>());
    
    
    public static async Task<AccountController> CreateAccountController(UserManager<ApplicationUser> userManager,
        IEmailSender<ApplicationUser> emailSender)
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        return CreateAccountController(userManager, db, emailSender);
    }
    
    public static async Task<AccountController> CreateAccountController(UserManager<ApplicationUser> userManager)
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        return CreateAccountController(userManager, db);
    }

    public static async Task<AccountController> CreateAccountControllerWithTokensProvider(ITokensProvider tokensProvider)
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationContext(dbOptions);
        var userManagerMock = UserManagerMockHelper.GetUserManagerMock<ApplicationUser>();
        return CreateAccountController(userManagerMock.Object, db, tokensProvider);
    }
    
    public static async Task<AccountController> CreateAccountController()
        => await CreateAccountController(UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object);
    
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