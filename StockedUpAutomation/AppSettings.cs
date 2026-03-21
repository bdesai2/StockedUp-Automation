namespace StockedUpAutomation;

/// <summary>
/// Strongly-typed wrapper around appsettings.json.
/// </summary>
public class AppSettings
{
    public YouTubeSettings YouTube { get; set; } = new();
    public AnthropicSettings Anthropic { get; set; } = new();
    public GmailSettings Gmail { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
    public PythonSettings Python { get; set; } = new();
    public DuplicateTrackingSettings DuplicateTracking { get; set; } = new();
}

public class PythonSettings
{
    public string ExecutablePath { get; set; } = "python";
}

public class YouTubeSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
}

public class AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class GmailSettings
{
    public string Address { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
}

public class OutputSettings
{
    public string Directory { get; set; } = ".";
}

public class DuplicateTrackingSettings
{
    /// <summary>
    /// Enable duplicate video detection. Set to false during troubleshooting to allow re-processing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the JSON file that stores the last exported video info.
    /// Defaults to "last_exported_video.json" in the output directory.
    /// </summary>
    public string StateFilePath { get; set; } = string.Empty;
}
