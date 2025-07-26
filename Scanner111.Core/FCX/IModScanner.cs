using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scanner111.Core.FCX
{
    public interface IModScanner
    {
        Task<ModScanResult> ScanUnpackedModsAsync(string modPath, IProgress<string>? progress = null, CancellationToken ct = default);
        Task<ModScanResult> ScanArchivedModsAsync(string modPath, IProgress<string>? progress = null, CancellationToken ct = default);
        Task<ModScanResult> ScanAllModsAsync(string modPath, IProgress<string>? progress = null, CancellationToken ct = default);
    }

    public class ModScanResult
    {
        public List<ModIssue> Issues { get; } = new();
        public List<string> CleanedFiles { get; } = new();
        public int TotalFilesScanned { get; set; }
        public int TotalArchivesScanned { get; set; }
        public TimeSpan ScanDuration { get; set; }
    }

    public class ModIssue
    {
        public ModIssueType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AdditionalInfo { get; set; }
    }

    public enum ModIssueType
    {
        TextureDimensionsInvalid,
        TextureFormatIncorrect,
        SoundFormatIncorrect,
        XseScriptFile,
        PrevisFile,
        AnimationData,
        ArchiveFormatIncorrect,
        CleanupFile
    }
}