using System;
using System.Globalization;
using Scanner111.GUI.Converters;
using Xunit;

namespace Scanner111.Tests.GUI.Converters;

public class BooleanToFindingsColorConverterTests
{
    private readonly BooleanToFindingsColorConverter _converter = new();

    [Theory]
    [InlineData(true, "#FFFF9500")]  // Orange for issues
    [InlineData(false, "#FF4CAF50")] // Green for no issues
    public void Convert_ValidBoolean_ReturnsCorrectColor(bool hasFindings, string expectedColor)
    {
        // Act
        var result = _converter.Convert(hasFindings, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expectedColor, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a boolean")]
    [InlineData(123)]
    [InlineData(3.14)]
    public void Convert_InvalidValue_ReturnsGrayColor(object? value)
    {
        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("#FF666666", result); // Gray for unknown
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack("#FFFF9500", typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = BooleanToFindingsColorConverter.Instance;
        var instance2 = BooleanToFindingsColorConverter.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }
}