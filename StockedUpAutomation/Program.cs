using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockedUpAutomation;
using System.Diagnostics;

// ── Bootstrap ────────────────────────────────────────────────────────────────

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()   // env vars override appsettings (useful for CI/cloud later)
    .Build();

var settings = new AppSettings();
config.GetSection("YouTube").Bind(settings.YouTube);
config.GetSection("Anthropic").Bind(settings.Anthropic);
config.GetSection("Gmail").Bind(settings.Gmail);
config.GetSection("Output").Bind(settings.Output);
config.GetSection("Python").Bind(settings.Python);
config.GetSection("DuplicateTracking").Bind(settings.DuplicateTracking);

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(opts =>
        {
            opts.TimestampFormat    = "HH:mm:ss  ";
            opts.SingleLine         = true;
            opts.IncludeScopes      = false;
        });
});

var logger = loggerFactory.CreateLogger("Main");

// ── Services ─────────────────────────────────────────────────────────────────

using var youTubeService    = new YouTubeService(settings.YouTube, settings.Python, loggerFactory.CreateLogger<YouTubeService>());
var trackingService   = new VideoTrackingService(settings.DuplicateTracking, settings.Output, loggerFactory.CreateLogger<VideoTrackingService>());
var reportGenerator   = new ReportGeneratorService(settings.Anthropic, loggerFactory.CreateLogger<ReportGeneratorService>());
var pdfBuilder        = new PdfBuilderService(loggerFactory.CreateLogger<PdfBuilderService>());
var dashboardBuilder  = new DashboardBuilderService(loggerFactory.CreateLogger<DashboardBuilderService>());
var reportIndex       = new ReportIndexService(loggerFactory.CreateLogger<ReportIndexService>());
var emailService      = new EmailService(settings.Gmail, loggerFactory.CreateLogger<EmailService>());

// ── Main flow ─────────────────────────────────────────────────────────────────

logger.LogInformation("══════════════════════════════════════════════════════════");
logger.LogInformation("StockedUp Automation — Starting");
logger.LogInformation("══════════════════════════════════════════════════════════");

try
{
    // ── STEP 1: Trading day check ─────────────────────────────────────────────
    var today = DateTime.Today.AddDays(1); //Check tomorrow's date to ensure report is ready by market open
    logger.LogInformation("Today is: {Date}", today.ToString("dddd, MMMM dd, yyyy"));

    if (!TradingCalendar.IsTradingDay(today))
    {
        logger.LogInformation("Not a trading day. Exiting — no report generated.");
        return 0;
    }
    logger.LogInformation("✓ Trading day confirmed.");

    // ── STEP 2: Get latest video ──────────────────────────────────────────────
    logger.LogInformation("Fetching latest Stocked Up video...");
    var videoInfo = await youTubeService.GetLatestVideoAsync();
    logger.LogInformation("✓ Video: {Title}", videoInfo.Title);

    // ── STEP 2.5: Check for duplicates ────────────────────────────────────
    if (await trackingService.IsAlreadyExportedAsync(videoInfo))
    {
        logger.LogInformation("This video has already been processed. Exiting — no report generated.");
        logger.LogInformation("To disable duplicate checking, set 'DuplicateTracking:Enabled' to false in appsettings.json");
        return 0;
    }

    // ── STEP 3: Get transcript ────────────────────────────────────────────────
    logger.LogInformation("Fetching transcript...");
    var transcript = await youTubeService.GetTranscriptAsync(videoInfo.VideoId);
    logger.LogInformation("✓ Transcript fetched ({Chars} characters)", transcript.Length);

    // ── STEP 4: Generate report via Claude ────────────────────────────────────
    logger.LogInformation("Sending transcript to Claude API...");
    var reportText = await reportGenerator.GenerateReportAsync(transcript, videoInfo.Title);
    logger.LogInformation("✓ Report generated.");

    // ── STEP 5: Build PDF ─────────────────────────────────────────────────────
    var dateSlug   = today.ToString("yyyy-MM-dd");
    var dayFolder  = Path.Combine(settings.Output.Directory, dateSlug);
    if (!Directory.Exists(dayFolder))
        Directory.CreateDirectory(dayFolder);

    // ── STEP 6: Build PDF ─────────────────────────────────────────────────────
    var pdfPath = Path.Combine(dayFolder, $"StockedUp_Report_{dateSlug}.pdf");
    logger.LogInformation("Building PDF...");
    pdfBuilder.BuildPdf(reportText, pdfPath, videoInfo.Title);
    logger.LogInformation("✓ PDF saved: {Path}", pdfPath);

    // ── STEP 7: Build HTML dashboard ──────────────────────────────────────────
    var dashboardPath = Path.Combine(dayFolder, $"dashboard_{dateSlug}.html");
    logger.LogInformation("Building HTML dashboard...");
    dashboardBuilder.BuildDashboard(reportText, dashboardPath, videoInfo.Title, today);
    logger.LogInformation("✓ Dashboard saved: {Path}", dashboardPath);

    // ── STEP 8: Rebuild master index ──────────────────────────────────────────
    logger.LogInformation("Updating report index...");
    reportIndex.RebuildIndex(settings.Output.Directory);
    logger.LogInformation("✓ Index updated.");

    // ── STEP 6: Send email ────────────────────────────────────────────────────
    logger.LogInformation("Sending email...");
    await emailService.SendReportAsync(pdfPath, dashboardPath, videoInfo.Title);
    logger.LogInformation("✓ Email sent to: {Recipient}", settings.Gmail.RecipientEmail);

    // ── STEP 7: Mark as exported ──────────────────────────────────────────
    logger.LogInformation("Opening dashboard in browser...");
    Process.Start(new ProcessStartInfo
    {
        FileName        = dashboardPath,
        UseShellExecute = true   // lets Windows pick the default browser
    });

    logger.LogInformation("══════════════════════════════════════════════════════════");
    logger.LogInformation("StockedUp Automation — Complete ✓");
    logger.LogInformation("══════════════════════════════════════════════════════════");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Automation failed with an unhandled exception.");
    logger.LogInformation("══════════════════════════════════════════════════════════");
    logger.LogInformation("StockedUp Automation — FAILED ✗  (see error above)");
    logger.LogInformation("══════════════════════════════════════════════════════════");
    return 1;
}
