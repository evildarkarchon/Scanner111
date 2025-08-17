using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Core.Services;

public class PastebinServiceTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly PastebinService _service;
    private readonly string _testDirectory;

    public PastebinServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PastebinTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var logger = new NullLogger<PastebinService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _messageCapture = new TestMessageCapture();

        // Change working directory to test directory
        Directory.SetCurrentDirectory(_testDirectory);

        _service = new PastebinService(logger, _mockSettingsService.Object, _messageCapture);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
    }

    [Fact]
    public void IsValidPastebinInput_ValidUrl_ReturnsTrue()
    {
        // Arrange
        var validUrls = new[]
        {
            "https://pastebin.com/AbCd1234",
            "http://pastebin.com/XyZ5678",
            "https://pastebin.com/raw/Test123",
            "HTTPS://PASTEBIN.COM/ABC123" // Case insensitive
        };

        // Act & Assert
        foreach (var url in validUrls)
            _service.IsValidPastebinInput(url).Should().BeTrue($"URL '{url}' should be valid");
    }

    [Fact]
    public void IsValidPastebinInput_ValidId_ReturnsTrue()
    {
        // Arrange
        var validIds = new[]
        {
            "AbCd1234",
            "XyZ5678",
            "Test123",
            "a1B2c3D4"
        };

        // Act & Assert
        foreach (var id in validIds) _service.IsValidPastebinInput(id).Should().BeTrue($"ID '{id}' should be valid");
    }

    [Fact]
    public void IsValidPastebinInput_Invalid_ReturnsFalse()
    {
        // Arrange
        var invalidInputs = new[]
        {
            "",
            " ",
            null,
            "https://google.com/test",
            "not-a-valid-id!",
            "id with spaces",
            "https://pastebin.com/" // No ID
        };

        // Act & Assert
        foreach (var input in invalidInputs!)
            _service.IsValidPastebinInput(input).Should().BeFalse($"Input '{input}' should be invalid");
    }

    [Fact]
    public void ConvertToRawUrl_PlainId_ReturnsRawUrl()
    {
        // Arrange
        var id = "AbCd1234";
        var expected = "https://pastebin.com/raw/AbCd1234";

        // Act
        var result = _service.ConvertToRawUrl(id);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertToRawUrl_RegularUrl_ReturnsRawUrl()
    {
        // Arrange
        var url = "https://pastebin.com/AbCd1234";
        var expected = "https://pastebin.com/raw/AbCd1234";

        // Act
        var result = _service.ConvertToRawUrl(url);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertToRawUrl_AlreadyRawUrl_ReturnsSameUrl()
    {
        // Arrange
        var url = "https://pastebin.com/raw/AbCd1234";

        // Act
        var result = _service.ConvertToRawUrl(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public async Task FetchAndSaveAsync_EmptyInput_ReturnsNull()
    {
        // Arrange
        var emptyInputs = new[] { "", " ", null };

        // Act & Assert
        foreach (var input in emptyInputs!)
        {
            var result = await _service.FetchAndSaveAsync(input!);
            result.Should().BeNull($"Empty input '{input}' should return null");
        }
    }

    [Fact]
    public async Task FetchAndSaveAsync_InvalidInput_ReturnsNull()
    {
        // Arrange
        var invalidInput = "not-a-valid-id!";

        // Act
        var result = await _service.FetchAndSaveAsync(invalidInput);

        // Assert
        result.Should().BeNull();
        _messageCapture.ErrorMessages.Should().Contain(msg => msg.Contains("Invalid Pastebin"));
    }

    [Fact]
    public async Task FetchMultipleAsync_MultipleUrls_ReturnsCorrectDictionary()
    {
        // Arrange
        var urls = new[] { "test1", "test2", "invalid!" };

        // Act
        var results = await _service.FetchMultipleAsync(urls);

        // Assert
        results.Should().HaveCount(3);
        results.Should().ContainKey("test1");
        results.Should().ContainKey("test2");
        results.Should().ContainKey("invalid!");

        // Invalid input should have null result
        results["invalid!"].Should().BeNull();
    }

    [Fact]
    public async Task FetchMultipleAsync_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var urls = Array.Empty<string>();

        // Act
        var results = await _service.FetchMultipleAsync(urls);

        // Assert
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("AbCd1234", "AbCd1234")]
    [InlineData("https://pastebin.com/AbCd1234", "AbCd1234")]
    [InlineData("https://pastebin.com/raw/XyZ5678", "XyZ5678")]
    [InlineData("http://pastebin.com/Test123", "Test123")]
    public void ExtractPastebinId_VariousInputs_ExtractsCorrectId(string input, string expectedId)
    {
        // This tests the internal logic through ConvertToRawUrl
        var rawUrl = _service.ConvertToRawUrl(input);
        rawUrl.Should().Contain($"/raw/{expectedId}");
    }

    [Fact]
    public async Task FetchAndSaveAsync_Cancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _service.FetchAndSaveAsync("test123", cts.Token);

        // Assert
        result.Should().BeNull();
        _messageCapture.ErrorMessages.Should().Contain(msg => msg.Contains("cancelled") || msg.Contains("timed out"));
    }
}