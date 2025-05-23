using System;
using Microsoft.Extensions.Logging;

namespace Scanner111.Services
{
    /// <summary>
    /// Extension methods for ILogger to provide similar methods to the Python logger
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Logs an informational message
        /// </summary>
        public static void Info(this ILogger logger, string message)
        {
            logger.LogInformation(message);
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void Debug(this ILogger logger, string message)
        {
            logger.LogDebug(message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void Error(this ILogger logger, string message)
        {
            logger.LogError(message);
        }

        /// <summary>
        /// Logs an error message with an exception
        /// </summary>
        public static void Error(this ILogger logger, string message, Exception ex)
        {
            logger.LogError(ex, message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void Warning(this ILogger logger, string message)
        {
            logger.LogWarning(message);
        }
    }
}
