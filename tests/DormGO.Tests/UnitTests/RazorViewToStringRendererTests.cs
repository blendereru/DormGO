using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using DormGO.Services;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace DormGO.Tests.UnitTests;

public class RazorViewToStringRendererTests
{
    [Fact]
    public async Task RenderViewToStringAsync_WhenViewExists_ReturnsRenderedHtml()
    {
        // Arrange
        const string expectedHtml = "<p>Hello world</p>";
        var mockView = new Mock<IView>();
        mockView.Setup(v => v.RenderAsync(It.IsAny<ViewContext>()))
                .Callback<ViewContext>(ctx => ctx.Writer.Write(expectedHtml))
                .Returns(Task.CompletedTask);

        var mockViewEngine = new Mock<IRazorViewEngine>();
        mockViewEngine.Setup(e => e.GetView(null, "TestView", true))
                      .Returns(ViewEngineResult.Found("TestView", mockView.Object));

        var mockTempDataProvider = new Mock<ITempDataProvider>();
        var serviceProvider = new Mock<IServiceProvider>();

        var renderer = new RazorViewToStringRenderer(
            mockViewEngine.Object,
            mockTempDataProvider.Object,
            serviceProvider.Object
        );

        // Act
        var result = await renderer.RenderViewToStringAsync("TestView", new { Name = "John" });

        // Assert
        Assert.Equal(expectedHtml, result);
    }

    [Fact]
    public async Task RenderViewToStringAsync_WhenViewNotFound_ThrowsException()
    {
        // Arrange
        var mockViewEngine = new Mock<IRazorViewEngine>();
        mockViewEngine.Setup(e => e.GetView(null, "MissingView", true))
                      .Returns(ViewEngineResult.NotFound("MissingView", new[] { "MissingView" }));

        var mockTempDataProvider = new Mock<ITempDataProvider>();
        var serviceProvider = new Mock<IServiceProvider>();

        var renderer = new RazorViewToStringRenderer(
            mockViewEngine.Object,
            mockTempDataProvider.Object,
            serviceProvider.Object
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.RenderViewToStringAsync("MissingView", new { }));

        Assert.Contains("Couldn't find view 'MissingView'", ex.Message);
    }

    [Fact]
    public async Task RenderViewToStringAsync_PassesModelCorrectly()
    {
        // Arrange
        object? passedModel = null;

        var mockView = new Mock<IView>();
        mockView.Setup(v => v.RenderAsync(It.IsAny<ViewContext>()))
                .Callback<ViewContext>(vc =>
                {
                    passedModel = vc.ViewData.Model;
                    vc.Writer.Write("dummy");
                })
                .Returns(Task.CompletedTask);

        var mockViewEngine = new Mock<IRazorViewEngine>();
        mockViewEngine.Setup(e => e.GetView(null, "TestModelView", true))
                      .Returns(ViewEngineResult.Found("TestModelView", mockView.Object));

        var mockTempDataProvider = new Mock<ITempDataProvider>();
        var serviceProvider = new Mock<IServiceProvider>();

        var renderer = new RazorViewToStringRenderer(
            mockViewEngine.Object,
            mockTempDataProvider.Object,
            serviceProvider.Object
        );

        var model = new { Message = "Hello!" };

        // Act
        await renderer.RenderViewToStringAsync("TestModelView", model);

        // Assert
        Assert.NotNull(passedModel);
        Assert.Equal(model, passedModel);
    }
}