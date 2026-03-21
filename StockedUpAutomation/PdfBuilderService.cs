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
    private static readonly string TldrBg    = "#F5F5F5";
    private static readonly string TableHdr  = "#1A3C5E";
    private static readonly string TableAlt  = "#F0F4F8";
    private static readonly string MidGray   = "#666666";
    private static readonly string LightGray = "#CCCCCC";
    private static readonly string BullGreen = "#1E8449";
    private static readonly string BearRed   = "#C0392B";
    private static readonly string White     = "#FFFFFF";

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
        var tldr     = sections.GetValueOrDefault("TLDR", "").Trim();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0.75f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                // ── Header ───────────────────────────────────────────────────
                page.Header().Element(h =>
                    h.Background(Navy).Padding(8).AlignCenter()
                          .Text("STOCKED UP — DAILY MARKET ANALYST REPORT")
                     .FontColor(Colors.White).Bold().FontSize(11));

                // ── Footer ───────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(t =>
                    {
                    t.Span("For informational purposes only. Not financial advice.  |  Page ")
                            .FontSize(8).FontColor(MidGray);
                    t.CurrentPageNumber().FontSize(8).FontColor(MidGray);
                    });

                // ── Content ──────────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    // Title block
                    col.Item().AlignCenter().Text("Stocked Up")
                       .FontSize(26).Bold().FontColor(Navy);
                    col.Item().AlignCenter().Text("Daily Market Analyst Report")
                       .FontSize(12).FontColor(MidGray);
                    col.Item().AlignCenter().Text(today)
                       .FontSize(11).FontColor(MidGray);
                    if (!string.IsNullOrWhiteSpace(videoTitle))
                        col.Item().AlignCenter().Text($"\"{videoTitle}\"")
                           .FontSize(9).Italic().FontColor(MidGray);
                    col.Item().PaddingVertical(4)
                       .LineHorizontal(2).LineColor(Navy);

                    // ── TLDR ─────────────────────────────────────────────────
                    if (!string.IsNullOrWhiteSpace(tldr))
                    {
                        col.Item().Background(TldrBg)
                           .Border(1).BorderColor(LightGray)
                           .Padding(12).Column(tldrCol =>
                           {
                               tldrCol.Item()
                                      .Text("TL;DR — Market Outlook")
                                      .FontSize(11).Bold().FontColor(Navy);
                               tldrCol.Item().PaddingTop(4)
                                      .Text(tldr)
                                      .FontSize(10).LineHeight(1.5f).FontColor("#333333");
                           });
                    }

                    // Sections
                    foreach (var (key, label) in SectionLabels)
                    {
                        var content = sections.GetValueOrDefault(key, "");
                        if (string.IsNullOrWhiteSpace(content)) continue;

                        // Section header
                        col.Item().PaddingTop(10).Column(sc =>
                        {
                            sc.Item().Text($"{key.Replace("SECTION ", "")}. {label}")
                              .FontSize(13).Bold().FontColor(Navy);
                            sc.Item().PaddingTop(3).LineHorizontal(1.5f).LineColor(Navy);
                            sc.Item().PaddingTop(6).Element(e => RenderSection(e, key, content));
                        });
                    }

                    // Disclaimer
                    col.Item().PaddingTop(14).LineHorizontal(0.5f).LineColor(LightGray);
                    col.Item().PaddingTop(6).AlignCenter()
                       .Text("Generated automatically from publicly available video content. " +
                             "For informational and educational purposes only. Not financial advice.")
                       .FontSize(8).Italic().FontColor(MidGray);
                });
            });
        }).GeneratePdf(outputPath);

        _logger.LogInformation("PDF saved: {Path}", outputPath);
    }

    // ── Section router ───────────────────────────────────────────────────────

    private void RenderSection(IContainer container, string key, string content)
    {
        switch (key)
        {
            case "SECTION 1": RenderMacroEvents(container, content);   break;
            case "SECTION 2": RenderSentiment(container, content);     break;
            case "SECTION 3": RenderSpyTable(container, content);      break;
            case "SECTION 4": RenderSetups(container, content);        break;
            case "SECTION 5": RenderMomentumTable(container, content); break;
            case "SECTION 6": RenderBigMoney(container, content);      break;
            case "SECTION 7": RenderSummary(container, content);       break;
            default:          RenderGeneric(container, content);       break;
        }
    }

    // ── Section 1: Macro Events — titled topics with bullets ─────────────────

    private void RenderMacroEvents(IContainer container, string content)
    {
        // Parse into Topic blocks
        var topics = ParseTopics(content);

        container.Column(col =>
        {
            col.Spacing(8);
            foreach (var topic in topics)
            {
                col.Item().Column(tc =>
                {
                    // Topic title
                    tc.Item().Text(topic.Title).FontSize(11).Bold().FontColor(Blue);

                    // Bullets
                    foreach (var bullet in topic.Bullets)
                    {
                        tc.Item().Row(r =>
                        {
                            r.ConstantItem(10).PaddingTop(2)
                             .Text("•").FontSize(10).FontColor(Navy);
                            r.RelativeItem().PaddingLeft(4)
                             .Text(bullet).FontSize(10).LineHeight(1.4f);
                        });
                    }

                    // Tickers to watch
                    if (topic.Tickers.Count > 0)
                    {
                        tc.Item().PaddingTop(2).Row(r =>
                        {
                            r.ConstantItem(80).Text("Watch:")
                             .FontSize(9).Bold().FontColor(MidGray);
                            r.RelativeItem().Text(string.Join("  ", topic.Tickers))
                             .FontSize(9).Bold().FontColor(Blue);
                        });
                    }

                    // Thin divider between topics
                    tc.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(LightGray);
                });
            }
        });
    }

    // ── Section content renderer ─────────────────────────────────────────────

    private void RenderSentiment(IContainer container, string content)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            foreach (var line in content.Split('\n'))
            {
                var t = line.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;

                // Overall sentiment badge row
                if (t.StartsWith("SENTIMENT_OVERALL:", StringComparison.OrdinalIgnoreCase))
                {
                    var val   = t["SENTIMENT_OVERALL:".Length..].Trim();
                    var color = val.ToLower().Contains("bear") ? BearRed
                              : val.ToLower().Contains("bull") ? BullGreen : Navy;
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(110).Text("Overall Sentiment:")
                         .FontSize(10).Bold();
                        r.RelativeItem().Text(val)
                         .FontSize(10).Bold().FontColor(color);
                    });
                    col.Item().PaddingBottom(2).LineHorizontal(0.5f).LineColor(LightGray);
                    continue;
                }

                // Detect "FIELD: value" lines
                if (t.StartsWith("BULLET", StringComparison.OrdinalIgnoreCase))
                {
                    var text = t["BULLET".Length..].Trim();
                    col.Item().Row(r =>
                {
                        r.ConstantItem(10).PaddingTop(2)
                         .Text("•").FontSize(10).FontColor(Navy);
                        r.RelativeItem().PaddingLeft(4)
                         .Text(text).FontSize(10).LineHeight(1.4f);
                    });
                }
            }
        });
    }

    // ── Section 3: SPY Levels — table ────────────────────────────────────────

    private void RenderSpyTable(IContainer container, string content)
                    {
        var rows = content.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("LEVEL_ROW:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l["LEVEL_ROW:".Length..].Trim().Split('|'))
            .Where(p => p.Length >= 3)
            .Select(p => (Price: p[0].Trim(), Type: p[1].Trim(), Note: p[2].Trim()))
            .ToList();

        if (rows.Count == 0)
                        {
            RenderGeneric(container, content);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.2f); // Level
                c.RelativeColumn(2.2f); // Type
                c.RelativeColumn(5.6f); // Note
                        });

            // Header row
            var headers = new[] { "Level / Range", "Support / Resistance", "Condition & Notes" };
            table.Header(header =>
            {
                foreach (var hdr in headers)
                {
                    header.Cell().Background(TableHdr).Padding(6)
                        .Text(hdr).FontSize(9).Bold().FontColor(Colors.White);
                }
            });

            // Data rows
            for (int i = 0; i < rows.Count; i++)
            {
                var (price, type, note) = rows[i];
                var bg = i % 2 == 0 ? White : TableAlt;
                var tColor = type.ToLower().Contains("support") ? BullGreen
                           : type.ToLower().Contains("resist")  ? BearRed
                           : Navy;

                table.Cell().Background(bg).Padding(5)
                     .Text(price).FontSize(9).Bold();
                table.Cell().Background(bg).Padding(5)
                     .Text(type).FontSize(9).FontColor(tColor).Bold();
                table.Cell().Background(bg).Padding(5)
                     .Text(note).FontSize(9).LineHeight(1.3f);
            }
        });
    }

    // ── Section 4: Setups ─────────────────────────────────────────────────────

    private void RenderSetups(IContainer container, string content)
    {
        var setups  = ParseKeyValueBlocks(content, "TICKER");

        container.Column(col =>
        {
            col.Spacing(8);
            foreach (var setup in setups)
            {
                var dir     = setup.GetValueOrDefault("DIRECTION", "");
                var dirColor = dir.ToLower().Contains("bull") ? BullGreen : BearRed;

                col.Item().Border(0.5f).BorderColor(LightGray)
                   .Padding(10).Column(sc =>
                   {
                       // Ticker + direction badge
                       sc.Item().Row(r =>
                       {
                           r.RelativeItem().Text(
                               $"{setup.GetValueOrDefault("TICKER", "")}  —  " +
                               $"{setup.GetValueOrDefault("COMPANY", "")}")
                            .FontSize(11).Bold().FontColor(Navy);
                           r.ConstantItem(70).AlignRight()
                            .Text(dir).FontSize(9).Bold().FontColor(dirColor);
                       });

                       sc.Item().PaddingTop(4)
                         .Text(setup.GetValueOrDefault("THESIS", ""))
                         .FontSize(10).LineHeight(1.4f).Italic();

                       // Levels row
                       var levels = setup.GetValueOrDefault("KEY LEVELS", "");
                       if (!string.IsNullOrWhiteSpace(levels))
                       {
                           sc.Item().PaddingTop(5).Row(r =>
                           {
                               r.ConstantItem(70).Text("Key Levels:")
                                .FontSize(9).Bold().FontColor(MidGray);
                               r.RelativeItem().Text(levels)
                                .FontSize(9).FontColor(Blue);
                           });
                       }

                       var notes = setup.GetValueOrDefault("NOTES", "");
                       if (!string.IsNullOrWhiteSpace(notes))
                       {
                           sc.Item().PaddingTop(3).Row(r =>
                           {
                               r.ConstantItem(70).Text("Notes:")
                                .FontSize(9).Bold().FontColor(MidGray);
                               r.RelativeItem().Text(notes).FontSize(9);
                           });
                       }
                   });
            }
        });
    }

    // ── Section 5: Momentum — table ───────────────────────────────────────────

    private void RenderMomentumTable(IContainer container, string content)
    {
        var rows = content.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("MOMENTUM_ROW:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l["MOMENTUM_ROW:".Length..].Trim().Split('|'))
            .Where(p => p.Length >= 4)
            .Select(p => (
                Ticker:    p[0].Trim(),
                Company:   p[1].Trim(),
                Direction: p[2].Trim(),
                Level:     p[3].Trim(),
                Note:      p.Length > 4 ? p[4].Trim() : ""))
            .ToList();

        if (rows.Count == 0)
        {
            RenderGeneric(container, content);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(2.8f);
                c.RelativeColumn(1.5f);
                c.RelativeColumn(1.5f);
                c.RelativeColumn(3f);
            });

            var momentumHeaders = new[] { "Ticker", "Company", "Direction", "Breakout Level", "Notes" };
            table.Header(header =>
            {
                foreach (var hdr in momentumHeaders)
                {
                    header.Cell().Background(TableHdr).Padding(6)
                        .Text(hdr).FontSize(9).Bold().FontColor(Colors.White);
                }
            });

            for (int i = 0; i < rows.Count; i++)
            {
                var (ticker, company, dir, level, note) = rows[i];
                var bg = i % 2 == 0 ? Colors.White.ToString() : TableAlt;
                var dirColor = dir.ToLower().Contains("bull") ? BullGreen : BearRed;

                table.Cell().Background(bg).Padding(5).Text(ticker).FontSize(9).Bold().FontColor(Navy);
                table.Cell().Background(bg).Padding(5).Text(company).FontSize(9);
                table.Cell().Background(bg).Padding(5).Text(dir).FontSize(9).Bold().FontColor(dirColor);
                table.Cell().Background(bg).Padding(5).Text(level).FontSize(9).Bold();
                table.Cell().Background(bg).Padding(5).Text(note).FontSize(9).LineHeight(1.3f);
            }
        });
    }

    // ── Section 6: Big Money ──────────────────────────────────────────────────

    private void RenderBigMoney(IContainer container, string content)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split('\n'))
        {
            var t        = line.Trim();
            var colonIdx = t.IndexOf(':');
            if (colonIdx > 0 && colonIdx < 25)
                d[t[..colonIdx].Trim().ToUpper()] = t[(colonIdx + 1)..].Trim();
        }

        container.Column(col =>
        {
            col.Spacing(5);

            // Ticker + company header
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(
                    $"{d.GetValueOrDefault("TICKER", "")}  —  {d.GetValueOrDefault("COMPANY", "")}")
                 .FontSize(12).Bold().FontColor(Navy);
                r.ConstantItem(100).AlignRight()
                 .Text(d.GetValueOrDefault("TRADE TYPE", ""))
                 .FontSize(9).Bold().FontColor(Blue);
            });

            // Stats table
            var statRows = new[]
            {
                ("Structure",     d.GetValueOrDefault("STRUCTURE", "")),
                ("Capital at Risk", d.GetValueOrDefault("CAPITAL AT RISK", "")),
                ("Max Profit",    d.GetValueOrDefault("MAX PROFIT", "")),
                ("Risk / Reward", d.GetValueOrDefault("RISK/REWARD", "")),
                ("Stop Loss",     d.GetValueOrDefault("STOP LOSS", "")),
            };

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2f);
                    c.RelativeColumn(8f);
                });

                for (int i = 0; i < statRows.Length; i++)
                {
                    var bg = i % 2 == 0 ? TableAlt : White;
                    table.Cell().Background(bg).Padding(5)
                         .Text(statRows[i].Item1).FontSize(9).Bold().FontColor(MidGray);
                    table.Cell().Background(bg).Padding(5)
                         .Text(statRows[i].Item2).FontSize(9).Bold().FontColor(Navy);
                }
            });

            // Commentary
            var commentary = d.GetValueOrDefault("ANALYST COMMENTARY", "");
            if (!string.IsNullOrWhiteSpace(commentary))
            {
                col.Item().PaddingTop(4).Background("#EAF0F7").Padding(8)
                   .Text(commentary).FontSize(10).LineHeight(1.5f).Italic();
            }
        });
    }

    // ── Section 7: Summary ────────────────────────────────────────────────────

    private void RenderSummary(IContainer container, string content)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            foreach (var line in content.Split('\n'))
            {
                var t = line.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;

                if (t.StartsWith("BIAS:", StringComparison.OrdinalIgnoreCase))
                {
                    var val   = t["BIAS:".Length..].Trim();
                    var color = val.ToLower().Contains("bear") ? BearRed
                              : val.ToLower().Contains("bull") ? BullGreen : Navy;
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(40).Text("Bias:").FontSize(10).Bold();
                        r.RelativeItem().Text(val).FontSize(10).Bold().FontColor(color);
                    });
                    col.Item().PaddingBottom(2).LineHorizontal(0.5f).LineColor(LightGray);
                        continue;
                    }

                if (t.StartsWith("BULLET", StringComparison.OrdinalIgnoreCase))
                {
                    var text = t["BULLET".Length..].Trim();
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(10).PaddingTop(2)
                         .Text("•").FontSize(10).FontColor(Navy);
                        r.RelativeItem().PaddingLeft(4)
                         .Text(text).FontSize(10).LineHeight(1.4f);
                    });
                }
            }
        });
    }

    // ── Generic fallback renderer ─────────────────────────────────────────────

    private static void RenderGeneric(IContainer container, string content)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            foreach (var line in content.Split('\n'))
            {
                var t = line.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                col.Item().Text(t).FontSize(10).LineHeight(1.4f);
            }
        });
                }

                // Bullet lines

    private static List<TopicBlock> ParseTopics(string content)
    {
        var topics  = new List<TopicBlock>();
        TopicBlock? current = null;

        foreach (var line in content.Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            if (t.StartsWith("TOPIC:", StringComparison.OrdinalIgnoreCase))
                {
                if (current != null) topics.Add(current);
                current = new TopicBlock(t["TOPIC:".Length..].Trim());
                    continue;
                }

            if (current == null) continue;
                // Pipe-delimited table rows (momentum plays)
            if (t.StartsWith("BULLET", StringComparison.OrdinalIgnoreCase))
                {
                current.Bullets.Add(t["BULLET".Length..].Trim());
                    continue;
                }

            if (t.StartsWith("TICKER_WATCH", StringComparison.OrdinalIgnoreCase))
            {
                var tickers = t["TICKER_WATCH".Length..].Trim()
                    .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
                current.Tickers.AddRange(tickers);
            }
        }
                // Regular body text
        if (current != null) topics.Add(current);
        return topics;
    }

    private static List<Dictionary<string, string>> ParseKeyValueBlocks(string content, string startKey)
    {
        var blocks  = new List<Dictionary<string, string>>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in content.Split('\n'))
        {
            var t        = line.Trim();
            var colonIdx = t.IndexOf(':');
            if (colonIdx <= 0 || colonIdx >= 25) continue;

            var key = t[..colonIdx].Trim().ToUpper();
            var val = t[(colonIdx + 1)..].Trim();

            // New block starts when we see the startKey again
            if (key == startKey.ToUpper() && current.ContainsKey(startKey.ToUpper()))
            {
                blocks.Add(current);
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            current[key] = val;
    }

        if (current.Count > 0) blocks.Add(current);
        return blocks;
    }

    private static Dictionary<string, string> ParseSections(string reportText)
    {
        var sections     = new Dictionary<string, string>();
        var currentKey  = "HEADER";
        var currentLines = new List<string>();

        foreach (var line in reportText.Split('\n'))
        {
            var upper   = line.Trim().ToUpper();
            var matched = false;

            // Check for TLDR
            if (upper == "TLDR")
            {
                if (currentLines.Count > 0)
                    sections[currentKey] = string.Join("\n", currentLines).Trim();
                currentKey   = "TLDR";
                currentLines = new List<string>();
                matched      = true;
            }

            if (!matched)
            {
                for (int i = 1; i <= 7; i++)
            {
                    var key = $"SECTION {i}";
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
            }

            if (!matched) currentLines.Add(line);
        }

        if (currentLines.Count > 0)
            sections[currentKey] = string.Join("\n", currentLines).Trim();

        return sections;
    }

    private record TopicBlock(string Title)
    {
        public List<string> Bullets { get; } = new();
        public List<string> Tickers { get; } = new();
    }
}
