using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Logging;

namespace StockedUpAutomation;

/// <summary>
/// Builds a professionally formatted PDF from Claude's structured report text.
/// Uses QuestPDF — a fluent, open-source .NET PDF library.
/// </summary>
public class PdfBuilderService
{
    private readonly ILogger<PdfBuilderService> _logger;

    // ── Brand colours ────────────────────────────────────────────────────────
    private static readonly string Navy      = "#1A3C5E";
    private static readonly string Blue      = "#2E5F8A";
    private static readonly string LightBlue = "#EAF0F7";
    private static readonly string MidGray   = "#666666";
    private static readonly string LightGray = "#CCCCCC";

    private static readonly Dictionary<string, string> SectionLabels = new()
    {
        ["SECTION 1"] = "Market Events & Macro Overview",
        ["SECTION 2"] = "Market Sentiment",
        ["SECTION 3"] = "SPY Technical Levels",
        ["SECTION 4"] = "Featured Stock Setups",
        ["SECTION 5"] = "Momentum Plays",
        ["SECTION 6"] = "Big Money Trade",
        ["SECTION 7"] = "Daily Summary & Outlook",
    };

    public PdfBuilderService(ILogger<PdfBuilderService> logger)
    {
        _logger = logger;
        // Set QuestPDF license (Community is free for this use case)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Renders the report text into a PDF file at the specified path.
    /// </summary>
    public void BuildPdf(string reportText, string outputPath, string videoTitle)
    {
        var sections = ParseSections(reportText);
        var today    = DateTime.Today.ToString("dddd, MMMM dd, yyyy");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0.85f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                // ── Header ───────────────────────────────────────────────────
                page.Header().Element(header =>
                {
                    header.Background(Navy).Padding(8).AlignCenter()
                          .Text("STOCKED UP — DAILY MARKET ANALYST REPORT")
                          .FontColor(Colors.White).Bold().FontSize(11);
                });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer().AlignCenter()
                    .Text(text =>
                    {
                        text.Span("For informational purposes only. Not financial advice.  |  Page ")
                            .FontSize(8).FontColor(MidGray);
                        text.CurrentPageNumber().FontSize(8).FontColor(MidGray);
                    });

                // ── Content ──────────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    col.Spacing(4);

                    // Title block
                    col.Item().AlignCenter().Text("Stocked Up")
                       .FontSize(28).Bold().FontColor(Navy);
                    col.Item().AlignCenter().Text("Daily Market Analyst Report")
                       .FontSize(12).FontColor(MidGray);
                    col.Item().AlignCenter().Text(today)
                       .FontSize(11).FontColor(MidGray);

                    if (!string.IsNullOrWhiteSpace(videoTitle))
                    {
                        col.Item().AlignCenter().Text($"Source: \"{videoTitle}\"")
                           .FontSize(9).Italic().FontColor(MidGray);
                    }

                    col.Item().PaddingVertical(6)
                       .LineHorizontal(2).LineColor(Navy);

                    // Sections
                    foreach (var (key, label) in SectionLabels)
                    {
                        var content = sections.TryGetValue(key, out var c) ? c : "No data extracted for this section.";
                        var number  = key.Replace("SECTION ", "");

                        // Section header
                        col.Item().PaddingTop(12).Column(sectionCol =>
                        {
                            sectionCol.Item()
                                      .Text($"{number}.  {label}")
                                      .FontSize(14).Bold().FontColor(Navy);
                            sectionCol.Item().PaddingTop(3)
                                      .LineHorizontal(1.5f).LineColor(Navy);
                            sectionCol.Item().PaddingTop(6)
                                      .Element(e => RenderSectionContent(e, content));
                        });
                    }

                    // Disclaimer
                    col.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor(LightGray);
                    col.Item().PaddingTop(6).AlignCenter()
                       .Text("This report is generated automatically from publicly available video content " +
                             "and is for informational and educational purposes only. " +
                             "It does not constitute financial advice. " +
                             "Always conduct your own due diligence before making any trading decisions.")
                       .FontSize(8).Italic().FontColor(MidGray);
                });
            });
        })
        .GeneratePdf(outputPath);

        _logger.LogInformation("PDF saved to: {Path}", outputPath);
    }

    // ── Section content renderer ─────────────────────────────────────────────

    private static void RenderSectionContent(IContainer container, string content)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    col.Item().PaddingVertical(2).Text("");
                    continue;
                }

                // Detect "FIELD: value" lines
                if (trimmed.Contains(':') && !trimmed.StartsWith('-') && !trimmed.StartsWith('•'))
                {
                    var colonIdx = trimmed.IndexOf(':');
                    var fieldKey = trimmed[..colonIdx].Trim();

                    if (fieldKey.Length < 25 && fieldKey == fieldKey.ToUpper())
                    {
                        var value = trimmed[(colonIdx + 1)..].Trim();
                        col.Item().Text(text =>
                        {
                            text.Span($"{fieldKey}: ").Bold().FontColor(Blue);
                            text.Span(value);
                        });
                        continue;
                    }
                }

                // Bullet lines
                if (trimmed.StartsWith('-') || trimmed.StartsWith('•'))
                {
                    col.Item().Text($"• {trimmed.TrimStart('-', '•').Trim()}");
                    continue;
                }

                // Pipe-delimited table rows (momentum plays)
                if (trimmed.Contains('|'))
                {
                    col.Item().Background(LightBlue).Padding(3)
                       .Text(trimmed).FontSize(9);
                    continue;
                }

                // Regular body text
                col.Item().Text(trimmed);
            }
        });
    }

    // ── Section parser ───────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseSections(string reportText)
    {
        var sections    = new Dictionary<string, string>();
        var currentKey  = "HEADER";
        var currentLines = new List<string>();

        foreach (var line in reportText.Split('\n'))
        {
            var upper   = line.Trim().ToUpper();
            var matched = false;

            foreach (var key in SectionLabels.Keys)
            {
                if (upper.StartsWith(key))
                {
                    if (currentLines.Count > 0)
                        sections[currentKey] = string.Join("\n", currentLines).Trim();

                    currentKey   = key;
                    currentLines = new List<string>();
                    matched      = true;
                    break;
                }
            }

            if (!matched)
                currentLines.Add(line);
        }

        if (currentLines.Count > 0)
            sections[currentKey] = string.Join("\n", currentLines).Trim();

        return sections;
    }
}
