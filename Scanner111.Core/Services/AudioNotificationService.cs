using System.Media;
using System.Reflection;
using NAudio.Wave;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

public class AudioNotificationService : IAudioNotificationService, IDisposable
{
    private readonly Dictionary<NotificationType, string> _customSounds = new();
    private readonly SemaphoreSlim _settingsSemaphore = new(1, 1);
    private readonly IApplicationSettingsService? _settingsService;
    private AudioFileReader? _audioFileReader;
    private bool _isEnabled;
    private float _volume = 0.5f;
    private WaveOutEvent? _waveOut;

    public AudioNotificationService(IApplicationSettingsService? settingsService = null)
    {
        _settingsService = settingsService;
        if (_settingsService != null)
            // Initialize synchronously for constructor, but don't block
            _ = InitializeAsync();
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            _ = SaveSettingAsync(settings => { settings.EnableAudioNotifications = value; });
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            var clampedValue = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_volume - clampedValue) < 0.001f) return;
            _volume = clampedValue;
            _ = SaveSettingAsync(settings => { settings.AudioVolume = _volume; });
        }
    }

    public async Task PlayScanCompleteAsync()
    {
        if (!IsEnabled) return;

        if (_customSounds.TryGetValue(NotificationType.ScanComplete, out var customSound))
            await PlayCustomSoundAsync(customSound).ConfigureAwait(false);
        else
            // Play default system sound as fallback
            PlaySystemSound(SystemSounds.Asterisk);
    }

    public async Task PlayErrorFoundAsync()
    {
        if (!IsEnabled) return;

        if (_customSounds.TryGetValue(NotificationType.ErrorFound, out var customSound))
            await PlayCustomSoundAsync(customSound).ConfigureAwait(false);
        else
            // Play default system sound as fallback
            PlaySystemSound(SystemSounds.Exclamation);
    }

    public async Task PlayCriticalIssueAsync()
    {
        if (!IsEnabled) return;

        if (_customSounds.TryGetValue(NotificationType.CriticalIssue, out var customSound))
            await PlayCustomSoundAsync(customSound).ConfigureAwait(false);
        else
            // Play default system sound as fallback  
            PlaySystemSound(SystemSounds.Hand);
    }

    public async Task PlayCustomSoundAsync(string filePath)
    {
        if (!IsEnabled || string.IsNullOrEmpty(filePath)) return;

        try
        {
            if (!File.Exists(filePath))
            {
                // Try to find the file in the application directory
                var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var alternativePath = Path.Combine(appDir!, "Resources", "Sounds", Path.GetFileName(filePath));
                if (File.Exists(alternativePath))
                    filePath = alternativePath;
                else
                    return; // File not found
            }

            // Use TaskCompletionSource to properly await audio playback
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                // Dispose previous instances
                DisposeAudioResources();

                _audioFileReader = new AudioFileReader(filePath)
                {
                    Volume = _volume
                };
                _waveOut = new WaveOutEvent();

                // Set up completion handler
                _waveOut.PlaybackStopped += (sender, args) => { tcs.TrySetResult(true); };

                _waveOut.Init(_audioFileReader);
                _waveOut.Play();

                // Wait for playback to complete
                await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                // Fallback to system sound on error
                PlaySystemSound(SystemSounds.Asterisk);
                tcs.TrySetResult(false);
            }
            finally
            {
                DisposeAudioResources();
            }
        }
        catch
        {
            // Silently fail if audio playback fails
        }
    }

    public void SetCustomSound(NotificationType type, string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            _customSounds.Remove(type);
        else
            _customSounds[type] = filePath;

        // Save to settings asynchronously
        _ = SaveSettingAsync(settings =>
        {
            settings.CustomNotificationSounds ??= new Dictionary<string, string>();
            if (string.IsNullOrEmpty(filePath))
                settings.CustomNotificationSounds.Remove(type.ToString());
            else
                settings.CustomNotificationSounds[type.ToString()] = filePath;
        });
    }

    public void Dispose()
    {
        DisposeAudioResources();
        _settingsSemaphore?.Dispose();
    }

    private async Task InitializeAsync()
    {
        if (_settingsService == null) return;

        try
        {
            var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            _isEnabled = settings.EnableAudioNotifications;
            _volume = settings.AudioVolume;

            // Load custom sounds from settings
            if (settings.CustomNotificationSounds != null)
                foreach (var kvp in settings.CustomNotificationSounds)
                    if (Enum.TryParse<NotificationType>(kvp.Key, out var type))
                        _customSounds[type] = kvp.Value;
        }
        catch
        {
            // Use defaults if settings fail to load
        }
    }

    private async Task SaveSettingAsync(Action<ApplicationSettings> updateAction)
    {
        if (_settingsService == null) return;

        await _settingsSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            updateAction(settings);
            await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(false);
        }
        catch
        {
            // Silently fail if settings save fails
        }
        finally
        {
            _settingsSemaphore.Release();
        }
    }

    private void PlaySystemSound(SystemSound sound)
    {
        try
        {
            sound?.Play();
        }
        catch
        {
            // Silently fail if system sound playback fails
        }
    }

    private void DisposeAudioResources()
    {
        _waveOut?.Dispose();
        _waveOut = null;
        _audioFileReader?.Dispose();
        _audioFileReader = null;
    }
}