using System;

namespace Scanner111.Models
{
    /// <summary>
    /// Represents the analysis result of a Papyrus log file
    /// </summary>
    public class PapyrusLogAnalysis
    {
        /// <summary>
        /// The path to the analyzed log file
        /// </summary>
        public string? LogFilePath { get; set; }

        /// <summary>
        /// Number of dumps found in the log file
        /// </summary>
        public int DumpCount { get; set; }

        /// <summary>
        /// Number of stacks found in the log file
        /// </summary>
        public int StackCount { get; set; }

        /// <summary>
        /// Number of warnings found in the log file
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// Number of errors found in the log file
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Ratio of dumps to stacks
        /// </summary>
        public float DumpStackRatio => StackCount == 0 ? 0 : (float)DumpCount / StackCount;

        /// <summary>
        /// Timestamp of when the analysis was performed
        /// </summary>
        public DateTime AnalysisTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Formatted message with analysis results
        /// </summary>
        public string FormattedMessage
        {
            get
            {
                if (string.IsNullOrEmpty(LogFilePath) || !System.IO.File.Exists(LogFilePath))
                {
                    return string.Join(Environment.NewLine, new[] {
                        "[!] ERROR : UNABLE TO FIND *Papyrus.0.log* (LOGGING IS DISABLED OR YOU DIDN'T RUN THE GAME)",
                        "ENABLE PAPYRUS LOGGING MANUALLY OR WITH BETHINI AND START THE GAME TO GENERATE THE LOG FILE",
                        "BethINI Link | Use Manual Download : https://www.nexusmods.com/site/mods/631?tab=files"
                    });
                }

                return string.Join(Environment.NewLine, new[] {
                    $"NUMBER OF DUMPS    : {DumpCount}",
                    $"NUMBER OF STACKS   : {StackCount}",
                    $"DUMPS/STACKS RATIO : {Math.Round(DumpStackRatio, 3)}",
                    $"NUMBER OF WARNINGS : {WarningCount}",
                    $"NUMBER OF ERRORS   : {ErrorCount}"
                });
            }
        }
    }
}
