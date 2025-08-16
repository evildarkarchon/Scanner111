namespace Scanner111.Core.Services;

public interface IAudioNotificationService
{
    bool IsEnabled { get; set; }
    float Volume { get; set; }

    Task PlayScanCompleteAsync();
    Task PlayErrorFoundAsync();
    Task PlayCustomSoundAsync(string filePath);
    void SetCustomSound(NotificationType type, string filePath);
}

public enum NotificationType
{
    ScanComplete,
    ErrorFound
}