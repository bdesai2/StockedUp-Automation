using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockedUpAutomation;

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

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(opts =>
        {
            opts.TimestampFormat    = "HH:mm:ss  ";
            opts.SingleLine         = true;
            opts.IncludeScopes      = false;
        });
});

var logger = loggerFactory.CreateLogger("Main");

// ── Services ─────────────────────────────────────────────────────────────────

var youTubeService    = new YouTubeService(settings.YouTube, settings.Python, loggerFactory.CreateLogger<YouTubeService>());
var reportGenerator   = new ReportGeneratorService(settings.Anthropic, loggerFactory.CreateLogger<ReportGeneratorService>());
var pdfBuilder        = new PdfBuilderService(loggerFactory.CreateLogger<PdfBuilderService>());
var emailService      = new EmailService(settings.Gmail, loggerFactory.CreateLogger<EmailService>());

// ── Main flow ─────────────────────────────────────────────────────────────────

logger.LogInformation("══════════════════════════════════════════════════════════");
logger.LogInformation("StockedUp Automation — Starting");
logger.LogInformation("══════════════════════════════════════════════════════════");

try
{
    // ── STEP 1: Trading day check ─────────────────────────────────────────────
    var today = DateTime.Today;
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

    // ── STEP 3: Get transcript ────────────────────────────────────────────────
    logger.LogInformation("Fetching transcript...");
    var transcript = await youTubeService.GetTranscriptAsync(videoInfo.VideoId);
    logger.LogInformation("✓ Transcript fetched ({Chars} characters)", transcript.Length);

    // ── STEP 4: Generate report via Claude ────────────────────────────────────
    logger.LogInformation("Sending transcript to Claude API...");
    var reportText = await reportGenerator.GenerateReportAsync(transcript, videoInfo.Title);
    logger.LogInformation("✓ Report generated.");

    // ── STEP 5: Build PDF ─────────────────────────────────────────────────────
    var outputDir = settings.Output.Directory;
    if (!Directory.Exists(outputDir))
        Directory.CreateDirectory(outputDir);

    var pdfFileName = $"StockedUp_Report_{today:yyyy-MM-dd}.pdf";
    var pdfPath     = Path.Combine(outputDir, pdfFileName);

    logger.LogInformation("Building PDF...");
    pdfBuilder.BuildPdf(reportText, pdfPath, videoInfo.Title);
    logger.LogInformation("✓ PDF saved to: {Path}", pdfPath);

    // ── STEP 6: Send email ────────────────────────────────────────────────────
    logger.LogInformation("Sending email...");
    await emailService.SendReportAsync(pdfPath, videoInfo.Title);
    logger.LogInformation("✓ Email sent to: {Recipient}", settings.Gmail.RecipientEmail);

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
