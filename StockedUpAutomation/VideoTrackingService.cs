using Microsoft.Extensions.Logging;
using System.Text.Json;
using static StockedUpAutomation.OutputSettings;

namespace StockedUpAutomation;

/// <summary>
/// Manages persistent tracking of the last exported video to prevent duplicate processing.
/// </summary>
public class VideoTrackingService
{
    private readonly DuplicateTrackingSettings _settings;
    private readonly string _stateFilePath;
    private readonly ILogger<VideoTrackingService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public VideoTrackingService(
        DuplicateTrackingSettings settings,
        OutputSettings outputSettings,
        ILogger<VideoTrackingService> logger)
    {
        _settings = settings;
        _logger = logger;

        // Use configured path or default to output directory
        _stateFilePath = string.IsNullOrWhiteSpace(_settings.StateFilePath)
            ? Path.Combine(outputSettings.Directory, "last_exported_video.json")
            : _settings.StateFilePath;
    }

    /// <summary>
    /// Checks if duplicate tracking is enabled.
    /// </summary>
    public bool IsEnabled => _settings.Enabled;

    /// <summary>
    /// Checks if the video has already been exported.
    /// Returns false if tracking is disabled.
    /// </summary>
    public async Task<bool> IsAlreadyExportedAsync(VideoInfo videoInfo)
    {
        if (!_settings.Enabled)
        {
            _logger.LogWarning("Duplicate tracking is DISABLED — allowing re-processing.");
            return false;
        }

        if (!File.Exists(_stateFilePath))
        {
            _logger.LogInformation("No previous export record found. This is a new video.");
            return false;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath);
            var lastExported = JsonSerializer.Deserialize<LastExportedVideo>(json);

            if (lastExported == null)
            {
                _logger.LogWarning("State file exists but is empty or invalid.");
                return false;
            }

            if (lastExported.VideoId == videoInfo.VideoId)
            {
                _logger.LogInformation(
                    "Duplicate detected: Video '{Title}' (ID: {VideoId}) was already exported on {ExportedAt}",
                    lastExported.Title,
                    lastExported.VideoId,
                    lastExported.ExportedAt);
                return true;
            }

            _logger.LogInformation(
                "New video detected. Last exported: '{LastTitle}' (ID: {LastVideoId}) on {ExportedAt}",
                lastExported.Title,
                lastExported.VideoId,
                lastExported.ExportedAt);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read state file. Assuming video is new.");
            return false;
        }
    }

    /// <summary>
    /// Records that a video has been successfully exported.
    /// Does nothing if tracking is disabled.
    /// </summary>
    public async Task MarkAsExportedAsync(VideoInfo videoInfo)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Duplicate tracking disabled — not saving state.");
            return;
        }

        try
        {
            var lastExported = new LastExportedVideo
            {
                VideoId = videoInfo.VideoId,
                Title = videoInfo.Title,
                PublishedAt = videoInfo.PublishedAt,
                ExportedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(lastExported, _jsonOptions);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_stateFilePath, json);

            _logger.LogInformation(
                "Marked video '{Title}' (ID: {VideoId}) as exported. State saved to: {Path}",
                videoInfo.Title,
                videoInfo.VideoId,
                _stateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save export state. Duplicate detection may not work on next run.");
        }
    }

    /// <summary>
    /// Gets information about the last exported video, if available.
    /// </summary>
    public async Task<LastExportedVideo?> GetLastExportedAsync()
    {
        if (!File.Exists(_stateFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath);
            return JsonSerializer.Deserialize<LastExportedVideo>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read last exported video info.");
            return null;
        }
    }
}