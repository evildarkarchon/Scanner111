using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.Reporting;

/// <summary>
/// Builds formatted markdown reports from ScanGame scan results.
/// </summary>
/// <remarks>
/// <para>
/// This service composes <see cref="ReportFragment"/>s from various ScanGame
/// result types into formatted markdown reports following CLASSIC conventions.
/// </para>
/// <para>
/// The builder delegates to <see cref="ScanGameSections"/> for individual
/// section generation and uses <see cref="ReportBuilder"/> for composition.
/// </para>
/// </remarks>
public sealed class ScanGameReportBuilder : IScanGameReportBuilder
{
    /// <inheritdoc/>
    public ReportFragment BuildUnpackedSection(UnpackedScanResult result, string xseAcronym)
    {
        if (!result.HasIssues) return new ReportFragment();

        var builder = new ReportBuilder();

        builder.Add(ScanGameSections.CreateUnpackedSectionHeader());
        builder.Add(ScanGameSections.CreateCleanupIssues(result.CleanupIssues));
        builder.Add(ScanGameSections.CreateAnimationDataIssues(result.AnimationDataIssues));
        builder.Add(ScanGameSections.CreateUnpackedTextureFormatIssues(result.TextureFormatIssues));
        builder.Add(ScanGameSections.CreateUnpackedTextureDimensionIssues(result.TextureDimensionIssues));
        builder.Add(ScanGameSections.CreateUnpackedSoundFormatIssues(result.SoundFormatIssues));
        builder.Add(ScanGameSections.CreateUnpackedXseFileIssues(result.XseFileIssues, xseAcronym));
        builder.Add(ScanGameSections.CreatePrevisFileIssues(result.PrevisFileIssues));

        return builder.Build();
    }

    /// <inheritdoc/>
    public ReportFragment BuildArchivedSection(BA2ScanResult result, string xseAcronym)
    {
        if (!result.HasIssues) return new ReportFragment();

        var builder = new ReportBuilder();

        builder.Add(ScanGameSections.CreateArchivedSectionHeader());
        builder.Add(ScanGameSections.CreateBA2FormatIssues(result.FormatIssues));
        builder.Add(ScanGameSections.CreateArchivedTextureFormatIssues(result.TextureFormatIssues));
        builder.Add(ScanGameSections.CreateArchivedTextureDimensionIssues(result.TextureDimensionIssues));
        builder.Add(ScanGameSections.CreateArchivedSoundFormatIssues(result.SoundFormatIssues));
        builder.Add(ScanGameSections.CreateArchivedXseFileIssues(result.XseFileIssues, xseAcronym));

        return builder.Build();
    }

    /// <inheritdoc/>
    public ReportFragment BuildIniSection(IniScanResult result)
    {
        if (!result.HasIssues) return new ReportFragment();

        return ScanGameSections.CreateConfigIssues(result.ConfigIssues);
    }

    /// <inheritdoc/>
    public ReportFragment BuildTomlSection(TomlScanResult result)
    {
        if (!result.HasIssues) return new ReportFragment();

        return ScanGameSections.CreateTomlIssues(result);
    }

    /// <inheritdoc/>
    public ReportFragment BuildXseSection(XseScanResult result, string xseAcronym)
    {
        if (!result.HasIssues) return new ReportFragment();

        return ScanGameSections.CreateXseStatusIssues(result, xseAcronym);
    }

    /// <inheritdoc/>
    public ReportFragment BuildIntegritySection(GameIntegrityResult result)
    {
        if (!result.HasIssues) return new ReportFragment();

        return ScanGameSections.CreateGameIntegrityIssues(result);
    }

    /// <inheritdoc/>
    public ReportFragment BuildCombinedReport(ScanGameReport report)
    {
        var builder = new ReportBuilder();

        // Main header
        builder.Add(ScanGameSections.CreateMainHeader(report.GameName, report.ScanTimestamp));

        // Unpacked file issues
        if (report.UnpackedResult != null)
        {
            builder.Add(BuildUnpackedSection(report.UnpackedResult, report.XseAcronym));
        }

        // Archived file issues
        if (report.ArchivedResult != null)
        {
            builder.Add(BuildArchivedSection(report.ArchivedResult, report.XseAcronym));
        }

        // Configuration section (INI + TOML)
        var hasConfigIssues = (report.IniResult?.HasIssues ?? false) || (report.TomlResult?.HasIssues ?? false);
        if (hasConfigIssues)
        {
            builder.Add(ScanGameSections.CreateConfigurationSectionHeader());

            if (report.IniResult != null)
            {
                builder.Add(BuildIniSection(report.IniResult));
            }

            if (report.TomlResult != null)
            {
                builder.Add(BuildTomlSection(report.TomlResult));
            }
        }

        // XSE status
        if (report.XseResult != null)
        {
            builder.Add(BuildXseSection(report.XseResult, report.XseAcronym));
        }

        // Game integrity
        if (report.IntegrityResult != null)
        {
            builder.Add(BuildIntegritySection(report.IntegrityResult));
        }

        // Success message if no issues found
        if (!report.HasAnyIssues && report.HasAnyResults)
        {
            builder.Add(ScanGameSections.CreateNoIssuesFound());
        }

        // Footer
        builder.Add(ScanGameSections.CreateFooter());

        return builder.Build();
    }
}
