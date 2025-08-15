using System.Globalization;
using FluentAssertions;
using Scanner111.GUI.Converters;

namespace Scanner111.Tests.GUI.Converters;

public class BooleanToFindingsColorConverterTests
{
    private readonly BooleanToFindingsColorConverter _converter = new();

    [Theory]
    [InlineData(true, "#FFFF9500")] // Orange for issues
    [InlineData(false, "#FF4CAF50")] // Green for no issues
    public void Convert_ValidBoolean_ReturnsCorrectColor(bool hasFindings, string expectedColor)
    {
        // Act
        var result = _converter.Convert(hasFindings, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expectedColor, "because boolean value should map to correct color");
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
        result.Should().Be("#FF666666", "because invalid values should return gray color");
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Assert
        var action = () => _converter.ConvertBack("#FFFF9500", typeof(bool), null, CultureInfo.InvariantCulture);
        action.Should().Throw<NotImplementedException>("because ConvertBack is not implemented");
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = BooleanToFindingsColorConverter.Instance;
        var instance2 = BooleanToFindingsColorConverter.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2, "because Instance should return singleton");
    }
}