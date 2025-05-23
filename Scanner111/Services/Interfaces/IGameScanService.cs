namespace Scanner111.Services.Interfaces;

/// <summary>
///     Interface for scanning game files and mods for issues.
/// </summary>
public interface IGameScanService
{
    /// <summary>
    ///     Inspects log files within a specified folder for recorded errors.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing log files for error inspection.</param>
    /// <returns>A detailed report of all detected errors in the relevant log files, if any.</returns>
    string CheckLogErrors(string folderPath);

    /// <summary>
    ///     Scans loose mod files for issues and moves redundant files to backup location.
    /// </summary>
    /// <returns>Detailed report of scan results.</returns>
    string? ScanModsUnpacked();

    /// <summary>
    ///     Scans archived BA2 mod files for issues.
    /// </summary>
    /// <returns>Detailed report of scan results.</returns>
    string? ScanModsArchived();

    /// <summary>
    ///     Manages game files by performing backup, restore, or removal operations.
    /// </summary>
    /// <param name="classicList">The name of the list specifying which files need to be managed.</param>
    /// <param name="mode">The operation mode to be performed on the files.</param>
    void GameFilesManage(string classicList, string mode = "BACKUP");

    /// <summary>
    ///     Generates a combined result summarizing game-related checks and scans.
    /// </summary>
    /// <returns>A string summarizing the results of all performed checks and scans.</returns>
    string GameCombinedResult();

    /// <summary>
    ///     Combines the results of scanning unpacked and archived mods.
    /// </summary>
    /// <returns>The combined results of the unpacked and archived mods scans.</returns>
    string? ModsCombinedResult();

    /// <summary>
    ///     Writes combined results of game and mods into a markdown report file.
    /// </summary>
    void WriteCombinedResults();
}