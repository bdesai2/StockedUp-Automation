namespace StockedUpAutomation;

/// <summary>
/// Represents the state of the last successfully exported video.
/// Persisted to JSON to prevent duplicate processing.
/// </summary>
public class LastExportedVideo
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PublishedAt { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; }
}