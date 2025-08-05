using System;
using System.Globalization;
using Scanner111.GUI.Converters;
using Xunit;

namespace Scanner111.Tests.GUI.Converters;

public class BooleanToFindingsTextConverterTests
{
    private readonly BooleanToFindingsTextConverter _converter = new();

    [Theory]
    [InlineData(true, "Issues Found")]
    [InlineData(false, "No Issues")]
    public void Convert_ValidBoolean_ReturnsCorrectText(bool hasFindings, string expectedText)
    {
        // Act
        var result = _converter.Convert(hasFindings, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expectedText, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a boolean")]
    [InlineData(123)]
    [InlineData(3.14)]
    public void Convert_InvalidValue_ReturnsUnknown(object? value)
    {
        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void Convert_ObjectValue_ReturnsUnknown()
    {
        // Arrange
        var value = new object();

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack("Issues Found", typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = BooleanToFindingsTextConverter.Instance;
        var instance2 = BooleanToFindingsTextConverter.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }
}