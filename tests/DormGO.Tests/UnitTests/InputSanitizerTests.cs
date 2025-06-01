using DormGO.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DormGO.Tests.UnitTests;

public class InputSanitizerTests
{
    [Fact]
    public void Sanitize_InvalidInput_ReturnsChangedValue()
    {
        // Arrange
        var badInputValue = " my \tbad input \nvalue ";
        var expectedValue = "my bad input value";
        var mockLogger = Mock.Of<ILogger<InputSanitizer>>();
        
        // Act
        var sanitizer = new InputSanitizer(mockLogger);
        
        // Assert
        Assert.Equal(expectedValue, sanitizer.Sanitize(badInputValue));
    }
    
    [Fact]
    public void Sanitize_ValidInput_ReturnsUnchangedValue()
    {
        // Arrange
        var inputValue = "my usual input value";
        var expectedValue = inputValue;
        var mockLogger = Mock.Of<ILogger<InputSanitizer>>();
        
        // Act
        var sanitizer = new InputSanitizer(mockLogger);
        
        // Assert
        Assert.Equal(expectedValue, sanitizer.Sanitize(inputValue));
    }
}