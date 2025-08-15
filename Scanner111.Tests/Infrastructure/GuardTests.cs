using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

public class GuardTests
{
    [Fact]
    public void NotNull_WithValidObject_ReturnsObject()
    {
        var testObject = "test string";
        var result = Guard.NotNull(testObject, nameof(testObject));
        Assert.Equal(testObject, result);
    }

    [Fact]
    public void NotNull_WithNull_ThrowsArgumentNullException()
    {
        string? nullString = null;
        var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(nullString!, "testParam"));
        Assert.Equal("testParam", ex.ParamName);
    }

    [Fact]
    public void NotNullOrEmpty_WithValidString_ReturnsString()
    {
        var testString = "test";
        var result = Guard.NotNullOrEmpty(testString, nameof(testString));
        Assert.Equal(testString, result);
    }

    [Fact]
    public void NotNullOrEmpty_WithNull_ThrowsArgumentNullException()
    {
        string? nullString = null;
        var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNullOrEmpty(nullString!, "testParam"));
        Assert.Equal("testParam", ex.ParamName);
    }

    [Fact]
    public void NotNullOrEmpty_WithEmptyString_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty("", "testParam"));
        Assert.Equal("testParam", ex.ParamName);
        Assert.Contains("Value cannot be empty", ex.Message);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithValidString_ReturnsString()
    {
        var testString = "test";
        var result = Guard.NotNullOrWhiteSpace(testString, nameof(testString));
        Assert.Equal(testString, result);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithNull_ThrowsArgumentNullException()
    {
        string? nullString = null;
        var ex = Assert.Throws<ArgumentNullException>(() => Guard.NotNullOrWhiteSpace(nullString!, "testParam"));
        Assert.Equal("testParam", ex.ParamName);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithWhiteSpace_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace("   ", "testParam"));
        Assert.Equal("testParam", ex.ParamName);
        Assert.Contains("Value cannot be empty or whitespace", ex.Message);
    }
}