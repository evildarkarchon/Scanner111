using System.Globalization;
using FluentAssertions;
using Scanner111.GUI.Converters;

namespace Scanner111.Tests.GUI.Converters;

[Collection("GUI Tests")]
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
        result.Should().Be(expectedText, "because boolean value should map to correct text");
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
        result.Should().Be("Unknown", "because invalid values should return Unknown");
    }

    [Fact]
    public void Convert_ObjectValue_ReturnsUnknown()
    {
        // Arrange
        var value = new object();

        // Act
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Unknown", "because invalid values should return Unknown");
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Assert
        var action = () => _converter.ConvertBack("Issues Found", typeof(bool), null, CultureInfo.InvariantCulture);
        action.Should().Throw<NotImplementedException>("because ConvertBack is not implemented");
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = BooleanToFindingsTextConverter.Instance;
        var instance2 = BooleanToFindingsTextConverter.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2, "because Instance should return singleton");
    }
}