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
}