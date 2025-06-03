using DormGO.Controllers;
using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    public static AccountController CreateAccountController(ApplicationContext db)
    {
        var accountController = new AccountController(
            UserManagerMockHelper.GetUserManagerMock<ApplicationUser>().Object,
            db,         
            Mock.Of<IEmailSender<ApplicationUser>>(),
            Mock.Of<ITokensProvider>(),
            Mock.Of<IUserHubNotificationService>(),
            Mock.Of<ILogger<AccountController>>(),
            Mock.Of<IInputSanitizer>());
        return CreateController(accountController);
    }

    public static PostController CreatePostController(ApplicationContext db)
    {
        var inputSanitizerMock = new Mock<IInputSanitizer>();
        inputSanitizerMock.Setup(x => x.Sanitize(It.IsAny<string>()))
            .Returns<string>(s => s);
        var postController = new PostController(
            db,
            Mock.Of<IPostHubNotificationService>(),
            Mock.Of<INotificationService<PostNotification, PostNotificationResponse>>(),
            Mock.Of<ILogger<PostController>>(),
            inputSanitizerMock.Object
            );
        return CreateController(postController);
    }
}