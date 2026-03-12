using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using static System.Net.Mime.MediaTypeNames;

namespace StockedUpAutomation;

public record VideoInfo(string VideoId, string Title, string PublishedAt);

/// <summary>
/// Fetches the latest video from a YouTube channel and retrieves its transcript.
/// Transcript fetching delegates to a Python sidecar (get_transcript.py)
/// which uses the youtube-transcript-api library — the most reliable 
/// solution available for this purpose.
/// </summary>
public class YouTubeService
{
    private readonly YouTubeSettings _settings;
    private readonly ILogger<YouTubeService> _logger;
    private readonly PythonSettings _python;

    public YouTubeService(YouTubeSettings settings, PythonSettings python, ILogger<YouTubeService> logger)
    {
        _settings = settings;
        _logger = logger;
        _python = python;
    }

    // ── Step 2: Get latest video ─────────────────────────────────────────────

    public async Task<VideoInfo> GetLatestVideoAsync()
    {
        var youtubeService = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = _settings.ApiKey,
            ApplicationName = "StockedUpAutomation"
        });

        var searchRequest = youtubeService.Search.List("snippet");
        searchRequest.ChannelId = _settings.ChannelId;
        searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        searchRequest.MaxResults = 1;
        searchRequest.Type = "video";

        var response = await searchRequest.ExecuteAsync();
        var item = response.Items.FirstOrDefault()
            ?? throw new InvalidOperationException("No videos found on the channel.");

        var info = new VideoInfo(
            VideoId: item.Id.VideoId,
            Title: item.Snippet.Title,
            PublishedAt: item.Snippet.PublishedAt?.ToString() ?? "Unknown"
        );

        _logger.LogInformation("Latest video found: {Title} (ID: {VideoId})",
            info.Title, info.VideoId);
        return info;
    }

    // ── Step 3: Get transcript via Python sidecar ────────────────────────────

    /// <summary>
    /// Calls get_transcript.py as a subprocess, passing the video ID.
    /// Python writes the full transcript to stdout; we capture and return it.
    /// Requires: Python installed + youtube-transcript-api pip package.
    /// </summary>
    public async Task<string> GetTranscriptAsync(string videoId)
    {
        _logger.LogInformation("Fetching transcript for video: {VideoId}", videoId);

        // get_transcript.py must sit in the same folder as the .exe
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "get_transcript.py");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException(
                $"get_transcript.py not found at: {scriptPath}\n" +
                "Copy get_transcript.py into the same folder as StockedUpAutomation.exe.");

        var psi = new ProcessStartInfo
        {
            FileName = _python.ExecutablePath, Arguments = $"\"{scriptPath}\" {videoId}",
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Transcript fetch failed (exit code {process.ExitCode}): {stderr.Trim()}");

        if (string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException(
                $"Transcript was empty for video {videoId}. " +
                "Captions may not be ready yet — try again in 1-2 hours after upload.");

        var transcript = stdout.Trim();
        _logger.LogInformation("Transcript fetched: {CharCount} characters", transcript.Length);
        return transcript;
    }
}