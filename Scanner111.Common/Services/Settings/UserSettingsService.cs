using System.Text.Json;
using Scanner111.Common.Models.Configuration;

namespace Scanner111.Common.Services.Settings;

/// <summary>
/// Service for reading and writing user settings to a JSON file.
/// Thread-safe and caches the loaded settings.
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private UserSettings? _cachedSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSettingsService"/> class.
    /// </summary>
    /// <param name="settingsFilePath">
    /// Optional custom path for the settings file.
    /// If not specified, uses the default location in AppData\Local\Scanner111.
    /// </param>
    public UserSettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? GetDefaultSettingsPath();
    }

    /// <inheritdoc/>
    public async Task<UserSettings> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _cachedSettings = UserSettings.Default;
                return _cachedSettings;
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions, ct)
                .ConfigureAwait(false);

            _cachedSettings = settings ?? UserSettings.Default;
            return _cachedSettings;
        }
        catch (JsonException)
        {
            // If the file is corrupted, return defaults
            _cachedSettings = UserSettings.Default;
            return _cachedSettings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(UserSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_settingsFilePath);
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct).ConfigureAwait(false);

            _cachedSettings = settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<UserSettings> GetCurrentAsync(CancellationToken ct = default)
    {
        if (_cachedSettings is not null)
        {
            return _cachedSettings;
        }

        return await LoadAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<UserSettings> UpdateAsync(Func<UserSettings, UserSettings> transform, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var current = await GetCurrentAsync(ct).ConfigureAwait(false);
        var updated = transform(current);
        await SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async Task<string?> GetCustomScanPathAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.CustomScanPath;
    }

    /// <inheritdoc/>
    public async Task SetCustomScanPathAsync(string? path, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { CustomScanPath = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetModsFolderPathAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.ModsFolderPath;
    }

    /// <inheritdoc/>
    public async Task SetModsFolderPathAsync(string? path, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { ModsFolderPath = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetIniFolderPathAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.IniFolderPath;
    }

    /// <inheritdoc/>
    public async Task SetIniFolderPathAsync(string? path, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { IniFolderPath = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetGameRootPathAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.GameRootPath;
    }

    /// <inheritdoc/>
    public async Task SetGameRootPathAsync(string? path, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { GameRootPath = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetDocumentsPathAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.DocumentsPath;
    }

    /// <inheritdoc/>
    public async Task SetDocumentsPathAsync(string? path, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { DocumentsPath = path }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> GetSelectedGameAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.SelectedGame ?? "Fallout4";
    }

    /// <inheritdoc/>
    public async Task SetSelectedGameAsync(string gameName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);
        await UpdateAsync(s => s with { SelectedGame = gameName }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> GetIsVrModeAsync(CancellationToken ct = default)
    {
        var settings = await GetCurrentAsync(ct).ConfigureAwait(false);
        return settings.IsVrMode;
    }

    /// <inheritdoc/>
    public async Task SetIsVrModeAsync(bool isVrMode, CancellationToken ct = default)
    {
        await UpdateAsync(s => s with { IsVrMode = isVrMode }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        _cachedSettings = null;
    }

    private static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(appData, "Scanner111", "settings.json");
    }
}
