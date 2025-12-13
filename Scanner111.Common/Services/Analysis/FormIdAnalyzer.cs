using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using Microsoft.Data.Sqlite;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Database;
using System.Data.Common;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Implementation of <see cref="IFormIdAnalyzer"/> using a database connection.
/// </summary>
public class FormIdAnalyzer : IFormIdAnalyzer
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private static readonly Regex FormIdRegex = new(
        @"\b(?:0x)?([0-9A-Fa-f]{8})\b",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="FormIdAnalyzer"/> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    public FormIdAnalyzer(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <inheritdoc/>
    public async Task<FormIdAnalysisResult> AnalyzeAsync(
        IReadOnlyList<LogSegment> segments,
        CancellationToken ct = default)
    {
        var foundFormIds = new HashSet<string>();

        foreach (var segment in segments)
        {
            // Skip segments that shouldn't be scanned for FormIDs to save time/noise
            if (segment.Name.Contains("Modules", StringComparison.OrdinalIgnoreCase)) continue;
            
            foreach (var line in segment.Lines)
            {
                var matches = FormIdRegex.Matches(line);
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        foundFormIds.Add(match.Groups[1].Value.ToUpperInvariant());
                    }
                }
            }
        }

        if (foundFormIds.Count == 0)
        {
            return new FormIdAnalysisResult();
        }

        var lookups = await LookupFormIdsAsync(foundFormIds.ToList(), ct).ConfigureAwait(false);

        return new FormIdAnalysisResult
        {
            DetectedRecords = lookups
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> LookupFormIdsAsync(
        IReadOnlyList<string> formIds,
        CancellationToken ct = default)
    {
        if (formIds == null || formIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(ct).ConfigureAwait(false);

            // Construct parameterized query manually since we aren't using Dapper
            using var command = connection.CreateCommand();
            var parameters = new List<string>();
            var sb = new StringBuilder();
            sb.Append("SELECT FormID, RecordName FROM FormIDDatabase WHERE FormID IN (");

            for (int i = 0; i < formIds.Count; i++)
            {
                var paramName = $"@id{i}";
                parameters.Add(paramName);

                var parameter = command.CreateParameter();
                parameter.ParameterName = paramName;
                parameter.Value = formIds[i];
                command.Parameters.Add(parameter);
            }

            sb.Append(string.Join(",", parameters));
            sb.Append(")");
            command.CommandText = sb.ToString();

            if (command is DbCommand dbCommand)
            {
                using var reader = await dbCommand.ExecuteReaderAsync(ct).ConfigureAwait(false);
                var results = new Dictionary<string, string>();

                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var formId = reader.GetString(0).ToUpperInvariant();
                    var recordName = reader.GetString(1);
                    if (!results.ContainsKey(formId))
                    {
                        results[formId] = recordName;
                    }
                }
                return results;
            }
            else
            {
                // Fallback for non-DbCommand (unlikely in typical ADO.NET but safe)
                using var reader = command.ExecuteReader();
                var results = new Dictionary<string, string>();

                while (reader.Read())
                {
                    var formId = reader.GetString(0).ToUpperInvariant();
                    var recordName = reader.GetString(1);
                    if (!results.ContainsKey(formId))
                    {
                        results[formId] = recordName;
                    }
                }
                return results;
            }
        }
        catch (SqliteException)
        {
            // Database errors (connection failed, table missing, etc.) - return empty result
            return new Dictionary<string, string>();
        }
        catch (InvalidOperationException)
        {
            // Connection state errors - return empty result
            return new Dictionary<string, string>();
        }
    }
}