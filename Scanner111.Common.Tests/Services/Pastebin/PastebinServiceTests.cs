using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Common.Services.Pastebin;
using Xunit;

namespace Scanner111.Common.Tests.Services.Pastebin;

/// <summary>
/// Tests for the PastebinService URL parsing and validation.
/// </summary>
public class PastebinServiceTests
{
    private readonly PastebinService _service;

    public PastebinServiceTests()
    {
        _service = new PastebinService(NullLogger<PastebinService>.Instance, new HttpClient());
    }

    #region IsValidInput Tests

    [Theory]
    [InlineData("https://pastebin.com/abc12345")]
    [InlineData("https://www.pastebin.com/abc12345")]
    [InlineData("http://pastebin.com/abc12345")]
    [InlineData("https://pastebin.com/raw/abc12345")]
    public void IsValidInput_WithValidPastebinComUrls_ReturnsTrue(string url)
    {
        _service.IsValidInput(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://paste.ee/p/abc12345")]
    [InlineData("https://www.paste.ee/p/abc12345")]
    [InlineData("https://paste.ee/r/abc12345")]
    public void IsValidInput_WithValidPasteEeUrls_ReturnsTrue(string url)
    {
        _service.IsValidInput(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://hastebin.com/abc12345")]
    [InlineData("https://hastebin.com/raw/abc12345")]
    [InlineData("https://haste.zneix.eu/abc12345")]
    [InlineData("https://haste.zneix.eu/raw/abc12345")]
    [InlineData("https://hastebin.com/abc12345.txt")]
    public void IsValidInput_WithValidHastebinUrls_ReturnsTrue(string url)
    {
        _service.IsValidInput(url).Should().BeTrue();
    }

    [Theory]
    [InlineData("abc12345")]
    [InlineData("AbCd1234")]
    [InlineData("testpaste")]
    public void IsValidInput_WithValidPasteIds_ReturnsTrue(string pasteId)
    {
        _service.IsValidInput(pasteId).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValidInput_WithEmptyOrWhitespace_ReturnsFalse(string? input)
    {
        _service.IsValidInput(input!).Should().BeFalse();
    }

    [Theory]
    [InlineData("abc")]  // Too short (less than 4 chars)
    [InlineData("abc-123")]  // Contains invalid characters
    [InlineData("abc_123")]  // Contains underscore
    [InlineData("https://invalid.com/abc")]  // Unsupported service
    [InlineData("not-a-url")]  // Contains hyphen
    public void IsValidInput_WithInvalidInputs_ReturnsFalse(string input)
    {
        _service.IsValidInput(input).Should().BeFalse();
    }

    #endregion

    #region PastebinFetchResult Tests

    [Fact]
    public void CreateSuccess_ReturnsSuccessResult()
    {
        var result = Scanner111.Common.Models.Pastebin.PastebinFetchResult.CreateSuccess(
            "content",
            "/path/to/file.log",
            "https://pastebin.com/abc123",
            "abc123");

        result.Success.Should().BeTrue();
        result.Content.Should().Be("content");
        result.SavedFilePath.Should().Be("/path/to/file.log");
        result.SourceUrl.Should().Be("https://pastebin.com/abc123");
        result.PasteId.Should().Be("abc123");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_ReturnsFailureResult()
    {
        var result = Scanner111.Common.Models.Pastebin.PastebinFetchResult.CreateFailure(
            "Network error",
            "https://pastebin.com/abc123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Network error");
        result.SourceUrl.Should().Be("https://pastebin.com/abc123");
        result.Content.Should().BeNull();
        result.SavedFilePath.Should().BeNull();
        result.PasteId.Should().BeNull();
    }

    #endregion

    #region FetchAsync Tests (Validation)

    [Fact]
    public async Task FetchAsync_WithEmptyUrl_ReturnsFailure()
    {
        var result = await _service.FetchAsync("");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task FetchAsync_WithWhitespaceUrl_ReturnsFailure()
    {
        var result = await _service.FetchAsync("   ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task FetchAsync_WithInvalidUrl_ReturnsFailure()
    {
        var result = await _service.FetchAsync("not-a-valid-url");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Could not parse");
    }

    #endregion
}
