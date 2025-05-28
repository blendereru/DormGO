using DormGO.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.UnitTests;

public class InputSanitizerTests
{
    [Fact]
    public void Sanitize_InvalidInput_ReturnsChangedValue()
    {
        var badInputValue = " my \tbad input \nvalue ";
        var expectedValue = "my bad input value";
        var mockLogger = Mock.Of<ILogger<InputSanitizer>>();
        var sanitizer = new InputSanitizer(mockLogger);
        
        Assert.Equal(expectedValue, sanitizer.Sanitize(badInputValue));
    }
    
    [Fact]
    public void Sanitize_ValidInput_ReturnsUnchangedValue()
    {
        var inputValue = "my usual input value";
        var expectedValue = inputValue;
        var mockLogger = Mock.Of<ILogger<InputSanitizer>>();
        var sanitizer = new InputSanitizer(mockLogger);
        
        Assert.Equal(expectedValue, sanitizer.Sanitize(inputValue));
    }
}