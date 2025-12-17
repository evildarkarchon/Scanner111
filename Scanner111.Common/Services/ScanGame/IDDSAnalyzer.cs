using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Provides functionality for analyzing and validating DDS (DirectDraw Surface) texture files.
/// </summary>
/// <remarks>
/// DDS is the standard texture format used by Fallout 4 and other DirectX games.
/// This analyzer can parse DDS headers, extract texture information, and validate
/// textures for game compatibility requirements.
/// </remarks>
public interface IDDSAnalyzer
{
    /// <summary>
    /// Analyzes a DDS file and extracts comprehensive texture information.
    /// </summary>
    /// <param name="filePath">The path to the DDS file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DDSInfo"/> containing texture details, or null if the file is not a valid DDS.</returns>
    Task<DDSInfo?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a DDS file from raw bytes (useful for analyzing archive contents).
    /// </summary>
    /// <param name="headerBytes">At least 128 bytes of the DDS file header.</param>
    /// <param name="fileSize">The total file size (for validation).</param>
    /// <returns>A <see cref="DDSInfo"/> containing texture details, or null if the data is not a valid DDS.</returns>
    DDSInfo? AnalyzeFromBytes(ReadOnlySpan<byte> headerBytes, long fileSize = 0);

    /// <summary>
    /// Validates a DDS texture for game compatibility.
    /// </summary>
    /// <param name="info">The DDS texture information to validate.</param>
    /// <param name="game">The target game (default is "Fallout4").</param>
    /// <returns>A <see cref="DDSValidationResult"/> containing any issues found.</returns>
    DDSValidationResult ValidateForGame(DDSInfo info, string game = "Fallout4");

    /// <summary>
    /// Checks if dimensions are valid for block-compressed textures.
    /// </summary>
    /// <param name="width">Texture width.</param>
    /// <param name="height">Texture height.</param>
    /// <returns>True if dimensions are valid for BC formats (multiples of 4).</returns>
    bool IsValidBCDimensions(int width, int height);
}
