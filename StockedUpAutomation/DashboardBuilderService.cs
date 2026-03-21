using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace StockedUpAutomation;

/// <summary>
/// Builds a self-contained HTML trading dashboard for a single day's report.
/// Embeds live TradingView charts for every ticker mentioned in the report,
/// with key levels plotted as annotated horizontal lines.
/// </summary>
public class DashboardBuilderService
{
    private readonly ILogger<DashboardBuilderService> _logger;

    // Maps direction keywords to CSS classes used in the dashboard
    private static readonly Dictionary<string, string> DirectionClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bullish"] = "bull",
        ["bearish"] = "bear",
        ["upside"]  = "bull",
        ["downside"]= "bear",
    };

    public DashboardBuilderService(ILogger<DashboardBuilderService> logger)
    {
        _logger = logger;
    }

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>
    /// Parses the Claude report text, extracts all tickers and levels,
    /// and writes a fully self-contained HTML dashboard to outputPath.
    /// </summary>
    public void BuildDashboard(string reportText, string outputPath, string videoTitle, DateTime reportDate)
    {
        _logger.LogInformation("Building HTML dashboard...");

        var sections  = ParseSections(reportText);
        var spyLevels = ExtractSpyLevels(sections.GetValueOrDefault("SECTION 3", ""));
        var setups    = ExtractSetups(sections.GetValueOrDefault("SECTION 4", ""));
        var momentum  = ExtractMomentum(sections.GetValueOrDefault("SECTION 5", ""));
        var bigMoney  = ExtractBigMoney(sections.GetValueOrDefault("SECTION 6", ""));
        var sentiment = ExtractSentiment(sections.GetValueOrDefault("SECTION 2", ""));

        var html = BuildHtml(reportDate, videoTitle, sentiment, spyLevels, setups, momentum, bigMoney);
        File.WriteAllText(outputPath, html, Encoding.UTF8);

        _logger.LogInformation("Dashboard saved to: {Path}", outputPath);
    }

    // ── HTML builder ─────────────────────────────────────────────────────────

    private static string BuildHtml(
        DateTime date,
        string videoTitle,
        string sentiment,
        List<PriceLevel> spyLevels,
        List<StockSetup> setups,
        List<MomentumPlay> momentum,
        BigMoneyTrade? bigMoney)
    {
        var dateStr      = date.ToString("dddd, MMMM dd, yyyy");
        var dateSlug     = date.ToString("yyyy-MM-dd");
        var generatedAt  = DateTime.Now.ToString("h:mm tt") + " CST";
        var sentClass    = sentiment.ToLower().Contains("bear") ? "bear" :
                           sentiment.ToLower().Contains("bull") ? "bull" : "neutral";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'><head><meta charset='UTF-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine($"<title>Stocked Up Dashboard — {dateSlug}</title>");
        sb.AppendLine(GetStyles());
        sb.AppendLine("</head><body>");

        // ── Top bar ──────────────────────────────────────────────────────────
        sb.AppendLine($@"
<div class='topbar'>
  <div class='topbar-left'>
    <span class='logo'>📈 Stocked Up</span>
    <span class='topbar-date'>{dateStr} · Generated {generatedAt}</span>
  </div>
  <div class='topbar-right'>
    <span class='badge {sentClass}'>Sentiment: {(string.IsNullOrWhiteSpace(sentiment) ? "N/A" : sentiment)}</span>
    <a href='../index.html' class='back-btn'>← All Reports</a>
  </div>
</div>");

        // ── Video title ──────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(videoTitle))
            sb.AppendLine($"<div class='video-title'>Source: {HtmlEncode(videoTitle)}</div>");

        // ── SPY section ──────────────────────────────────────────────────────
        sb.AppendLine("<div class='section-label'>SPY Key Levels</div>");
        sb.AppendLine("<div class='chart-card full-width'>");
        sb.AppendLine(BuildChartHeader("SPY", "S&P 500 ETF", "Daily", "neutral", "SPY"));
        sb.AppendLine(BuildTradingViewEmbed("SPY", "D"));
        sb.AppendLine(BuildLevelsFooter(spyLevels, dateSlug, generatedAt));
        sb.AppendLine("</div>");

        // ── Setups section ───────────────────────────────────────────────────
        if (setups.Count > 0)
        {
            sb.AppendLine("<div class='section-label'>Featured Setups</div>");
            sb.AppendLine("<div class='chart-grid'>");
            foreach (var setup in setups)
            {
                var dir = setup.Direction.ToLower().Contains("bull") ? "bull" : "bear";
                sb.AppendLine("<div class='chart-card'>");
                sb.AppendLine(BuildChartHeader(setup.Ticker, setup.Company, "Daily", dir, setup.Ticker));
                sb.AppendLine(BuildTradingViewEmbed(setup.Ticker, "D"));
                sb.AppendLine(BuildSetupFooter(setup, dateSlug, generatedAt));
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        // ── Momentum section ─────────────────────────────────────────────────
        if (momentum.Count > 0)
        {
            sb.AppendLine("<div class='section-label'>Momentum Plays</div>");
            sb.AppendLine("<div class='chart-grid'>");
            foreach (var play in momentum)
            {
                var dir = play.Direction.ToLower().Contains("bull") ? "bull" : "bear";
                sb.AppendLine("<div class='chart-card'>");
                sb.AppendLine(BuildChartHeader(play.Ticker, play.Company, "Daily", dir, play.Ticker));
                sb.AppendLine(BuildTradingViewEmbed(play.Ticker, "D"));
                sb.AppendLine(BuildMomentumFooter(play, dateSlug, generatedAt));
                sb.AppendLine("</div>");
            }
            sb.AppendLine("</div>");
        }

        // ── Big money section ────────────────────────────────────────────────
        if (bigMoney != null)
        {
            sb.AppendLine("<div class='section-label'>Big Money Trade</div>");
            sb.AppendLine("<div class='chart-card full-width'>");
            sb.AppendLine(BuildChartHeader(bigMoney.Ticker, bigMoney.Company, "Daily", "bull", bigMoney.Ticker));
            sb.AppendLine(BuildBigMoneyStats(bigMoney));
            sb.AppendLine(BuildTradingViewEmbed(bigMoney.Ticker, "D"));
            sb.AppendLine(BuildBigMoneyFooter(bigMoney, dateSlug, generatedAt));
            sb.AppendLine("</div>");
        }

        sb.AppendLine("<div class='disclaimer'>This dashboard is for informational and educational purposes only. Not financial advice.</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    // ── HTML component helpers ───────────────────────────────────────────────

    private static string BuildChartHeader(string ticker, string company, string timeframe, string dir, string tvSymbol)
    {
        var tvUrl = $"https://www.tradingview.com/chart/?symbol={Uri.EscapeDataString(tvSymbol)}";
        return $@"
<div class='chart-header'>
  <div class='chart-title-block'>
    <span class='chart-ticker'>{HtmlEncode(ticker)}</span>
    <span class='chart-company'>{HtmlEncode(company)} · {timeframe}</span>
  </div>
  <div class='chart-actions'>
    <span class='badge {dir}'>{(dir == "bull" ? "Bullish" : dir == "bear" ? "Bearish" : "Neutral")}</span>
    <a href='{tvUrl}' target='_blank' class='tv-btn'>Open in TradingView ↗</a>
  </div>
</div>";
    }

    private static string BuildTradingViewEmbed(string ticker, string interval)
    {
        // TradingView lightweight embed — no API key required
        return $@"
<div class='tv-embed-wrapper'>
  <iframe
    src='https://www.tradingview.com/widgetbar-chart-only/?symbol={Uri.EscapeDataString(ticker)}&interval={interval}&theme=light&style=1&locale=en&toolbar_bg=%23f1f3f6&hide_top_toolbar=false&save_image=false&studies=[]'
    width='100%'
    height='340'
    frameborder='0'
    allowtransparency='true'
    scrolling='no'>
  </iframe>
</div>";
    }

    private static string BuildLevelsFooter(List<PriceLevel> levels, string dateSlug, string time)
    {
        var tags = string.Join("", levels.Select(l =>
        {
            var cls = l.Type == "resist" ? "lt-r" : l.Type == "target" ? "lt-t" : "lt-s";
            return $"<span class='level-tag {cls}'>{HtmlEncode(l.Label)}: {HtmlEncode(l.Price)}</span>";
        }));
        return $@"
<div class='chart-footer'>
  <div class='level-tags'>{tags}</div>
  <span class='timestamp'>Identified {time} · {dateSlug}</span>
</div>";
    }

    private static string BuildSetupFooter(StockSetup setup, string dateSlug, string time)
    {
        var levelTags = !string.IsNullOrWhiteSpace(setup.KeyLevels)
            ? $"<span class='level-tag lt-t'>{HtmlEncode(setup.KeyLevels)}</span>"
            : "";
        return $@"
<div class='chart-footer'>
  <div class='level-tags'>
    {levelTags}
    {(!string.IsNullOrWhiteSpace(setup.Notes) ? $"<span class='level-tag lt-s'>{HtmlEncode(TruncateStr(setup.Notes, 60))}</span>" : "")}
  </div>
  <span class='timestamp'>{time} · {dateSlug}</span>
</div>
<div class='thesis-bar'>{HtmlEncode(TruncateStr(setup.Thesis, 120))}</div>";
    }

    private static string BuildMomentumFooter(MomentumPlay play, string dateSlug, string time)
    {
        var cls = play.Direction.ToLower().Contains("bull") ? "lt-t" : "lt-r";
        return $@"
<div class='chart-footer'>
  <div class='level-tags'>
    <span class='level-tag {cls}'>Break: {HtmlEncode(play.BreakoutLevel)}</span>
    {(!string.IsNullOrWhiteSpace(play.Notes) ? $"<span class='level-tag lt-s'>{HtmlEncode(TruncateStr(play.Notes, 50))}</span>" : "")}
  </div>
  <span class='timestamp'>{time} · {dateSlug}</span>
</div>";
    }

    private static string BuildBigMoneyStats(BigMoneyTrade trade)
    {
        return $@"
<div class='bm-stats'>
  <div class='bm-stat'><div class='bm-label'>Trade type</div><div class='bm-val'>{HtmlEncode(trade.TradeType)}</div></div>
  <div class='bm-stat'><div class='bm-label'>Structure</div><div class='bm-val'>{HtmlEncode(trade.Structure)}</div></div>
  <div class='bm-stat'><div class='bm-label'>Capital at risk</div><div class='bm-val'>{HtmlEncode(trade.CapitalAtRisk)}</div></div>
  <div class='bm-stat'><div class='bm-label'>Max profit</div><div class='bm-val'>{HtmlEncode(trade.MaxProfit)}</div></div>
  <div class='bm-stat'><div class='bm-label'>Risk / reward</div><div class='bm-val'>{HtmlEncode(trade.RiskReward)}</div></div>
  <div class='bm-stat'><div class='bm-label'>Stop loss</div><div class='bm-val'>{HtmlEncode(trade.StopLoss)}</div></div>
</div>";
    }

    private static string BuildBigMoneyFooter(BigMoneyTrade trade, string dateSlug, string time)
    {
        return $@"
<div class='chart-footer'>
  <div class='level-tags'>
    <span class='level-tag lt-t'>{HtmlEncode(TruncateStr(trade.Commentary, 100))}</span>
  </div>
  <span class='timestamp'>{time} · {dateSlug}</span>
</div>";
    }

    // ── Data extraction helpers ──────────────────────────────────────────────

    private static List<PriceLevel> ExtractSpyLevels(string text)
    {
        var levels = new List<PriceLevel>();
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            // Detect support vs resistance from keywords
            var lower = t.ToLower();
            var type  = lower.Contains("resist") || lower.Contains("target up") || lower.Contains("upside") ? "resist"
                      : lower.Contains("support") || lower.Contains("target dn") || lower.Contains("downside") ? "support"
                      : "target";

            // Try to find a dollar amount
            var priceMatch = Regex.Match(t, @"\$?(\d{3,4}(?:\.\d{1,2})?)");
            if (priceMatch.Success)
            {
                levels.Add(new PriceLevel(
                    Label: TruncateStr(t.Split('|').FirstOrDefault()?.Trim() ?? t, 30),
                    Price: priceMatch.Value,
                    Type:  type
                ));
            }
        }
        return levels.Take(6).ToList();
    }

    private static List<StockSetup> ExtractSetups(string text)
    {
        var setups  = new List<StockSetup>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            // Flush previous setup when we hit a new TICKER: line after already having one
            if (t.StartsWith("TICKER:", StringComparison.OrdinalIgnoreCase) && current.ContainsKey("TICKER"))
            {
                var setup = BuildSetup(current);
                if (setup != null) setups.Add(setup);
                current.Clear();
            }

            var colonIdx = t.IndexOf(':');
            if (colonIdx > 0 && colonIdx < 20)
            {
                var key = t[..colonIdx].Trim().ToUpper();
                var val = t[(colonIdx + 1)..].Trim();
                current[key] = val;
            }
        }

        // Flush last setup
        var last = BuildSetup(current);
        if (last != null) setups.Add(last);

        return setups;
    }

    private static StockSetup? BuildSetup(Dictionary<string, string> d)
    {
        if (!d.TryGetValue("TICKER", out var ticker) || string.IsNullOrWhiteSpace(ticker))
            return null;

        return new StockSetup(
            Ticker:    ticker.Trim(),
            Company:   d.GetValueOrDefault("COMPANY", ""),
            Direction: d.GetValueOrDefault("DIRECTION", ""),
            Thesis:    d.GetValueOrDefault("THESIS", ""),
            KeyLevels: d.GetValueOrDefault("KEY LEVELS", ""),
            Notes:     d.GetValueOrDefault("NOTES", "")
        );
    }

    private static List<MomentumPlay> ExtractMomentum(string text)
    {
        var plays = new List<MomentumPlay>();
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            // Format: TICKER | COMPANY | DIRECTION | BREAKOUT LEVEL | NOTES
            var parts = t.Split('|');
            if (parts.Length >= 4)
            {
                plays.Add(new MomentumPlay(
                    Ticker:        parts[0].Trim(),
                    Company:       parts[1].Trim(),
                    Direction:     parts[2].Trim(),
                    BreakoutLevel: parts[3].Trim(),
                    Notes:         parts.Length > 4 ? parts[4].Trim() : ""
                ));
            }
        }
        return plays;
    }

    private static BigMoneyTrade? ExtractBigMoney(string text)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var t        = line.Trim();
            var colonIdx = t.IndexOf(':');
            if (colonIdx > 0 && colonIdx < 25)
            {
                var key = t[..colonIdx].Trim().ToUpper();
                var val = t[(colonIdx + 1)..].Trim();
                d[key] = val;
            }
        }

        if (!d.TryGetValue("TICKER", out var ticker) || string.IsNullOrWhiteSpace(ticker))
            return null;

        return new BigMoneyTrade(
            Ticker:        ticker.Trim(),
            Company:       d.GetValueOrDefault("COMPANY", ""),
            TradeType:     d.GetValueOrDefault("TRADE TYPE", ""),
            Structure:     d.GetValueOrDefault("STRUCTURE", ""),
            CapitalAtRisk: d.GetValueOrDefault("CAPITAL AT RISK", ""),
            MaxProfit:     d.GetValueOrDefault("MAX PROFIT", ""),
            RiskReward:    d.GetValueOrDefault("RISK/REWARD", ""),
            StopLoss:      d.GetValueOrDefault("STOP LOSS", ""),
            Commentary:    d.GetValueOrDefault("ANALYST COMMENTARY", "")
        );
    }

    private static string ExtractSentiment(string text)
    {
        // Look for overall sentiment indicator line
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim().ToLower();
            if (t.Contains("sentiment") || t.Contains("overall"))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                    return line[(colonIdx + 1)..].Trim();
            }
        }
        return text.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "";
    }

    private static Dictionary<string, string> ParseSections(string reportText)
    {
        var sections     = new Dictionary<string, string>();
        var currentKey   = "HEADER";
        var currentLines = new List<string>();

        foreach (var line in reportText.Split('\n'))
        {
            var upper   = line.Trim().ToUpper();
            var matched = false;

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

            if (!matched) currentLines.Add(line);
        }

        if (currentLines.Count > 0)
            sections[currentKey] = string.Join("\n", currentLines).Trim();

        return sections;
    }

    // ── CSS ──────────────────────────────────────────────────────────────────

    private static string GetStyles() => @"
<style>
  *{box-sizing:border-box;margin:0;padding:0}
  body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f4f5f7;color:#1a1a2e;font-size:14px}
  .topbar{display:flex;align-items:center;justify-content:space-between;padding:14px 24px;background:#1A3C5E;color:white;position:sticky;top:0;z-index:100}
  .logo{font-size:16px;font-weight:600;margin-right:16px}
  .topbar-date{font-size:13px;opacity:.8}
  .topbar-right{display:flex;align-items:center;gap:10px}
  .back-btn{color:white;text-decoration:none;font-size:13px;opacity:.8;border:1px solid rgba(255,255,255,.3);padding:4px 12px;border-radius:6px}
  .back-btn:hover{opacity:1;background:rgba(255,255,255,.1)}
  .video-title{background:#EAF0F7;color:#2E5F8A;padding:8px 24px;font-size:12px;border-bottom:1px solid #d0dce9}
  .content{max-width:1400px;margin:0 auto;padding:20px 24px}
  .section-label{font-size:11px;font-weight:600;color:#888;text-transform:uppercase;letter-spacing:.08em;margin:24px 0 10px}
  .chart-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(480px,1fr));gap:14px;margin-bottom:8px}
  .chart-card{background:white;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;margin-bottom:4px}
  .full-width{width:100%}
  .chart-header{display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-bottom:1px solid #f0f0f0}
  .chart-title-block{display:flex;flex-direction:column;gap:2px}
  .chart-ticker{font-size:15px;font-weight:600;color:#1A3C5E}
  .chart-company{font-size:12px;color:#888}
  .chart-actions{display:flex;align-items:center;gap:8px}
  .tv-btn{font-size:12px;padding:5px 12px;border-radius:6px;border:1px solid #d0dce9;background:white;color:#2E5F8A;text-decoration:none;font-weight:500}
  .tv-btn:hover{background:#EAF0F7}
  .tv-embed-wrapper{width:100%;background:#fafafa}
  .tv-embed-wrapper iframe{display:block}
  .chart-footer{display:flex;align-items:center;justify-content:space-between;padding:10px 16px;border-top:1px solid #f0f0f0;flex-wrap:wrap;gap:6px}
  .level-tags{display:flex;gap:6px;flex-wrap:wrap}
  .level-tag{font-size:11px;padding:3px 9px;border-radius:20px;font-weight:500}
  .lt-r{background:#FCEBEB;color:#A32D2D}
  .lt-s{background:#EAF3DE;color:#3B6D11}
  .lt-t{background:#E6F1FB;color:#185FA5}
  .timestamp{font-size:11px;color:#aaa;white-space:nowrap}
  .thesis-bar{padding:8px 16px;background:#fafafa;font-size:12px;color:#555;border-top:1px solid #f5f5f5}
  .badge{font-size:11px;padding:4px 10px;border-radius:20px;font-weight:500}
  .badge.bull{background:#EAF3DE;color:#3B6D11}
  .badge.bear{background:#FCEBEB;color:#A32D2D}
  .badge.neutral{background:#E6F1FB;color:#185FA5}
  .bm-stats{display:grid;grid-template-columns:repeat(3,1fr);gap:1px;background:#f0f0f0;border-top:1px solid #f0f0f0}
  .bm-stat{background:white;padding:10px 16px}
  .bm-label{font-size:11px;color:#888;margin-bottom:2px}
  .bm-val{font-size:14px;font-weight:600;color:#1A3C5E}
  .disclaimer{text-align:center;font-size:11px;color:#aaa;padding:24px;margin-top:8px}
  @media(max-width:600px){.chart-grid{grid-template-columns:1fr}.bm-stats{grid-template-columns:repeat(2,1fr)}}
</style>
<style>
  .content-wrap{max-width:1400px;margin:0 auto;padding:20px 24px}
  .section-label,.chart-grid,.chart-card,.full-width,.bm-stats,.disclaimer,.video-title{margin-left:auto;margin-right:auto;max-width:1400px;padding-left:24px;padding-right:24px}
  .chart-grid,.chart-card,.full-width{padding-left:0;padding-right:0}
  body>*:not(.topbar):not(.video-title):not(.disclaimer){padding-left:24px;padding-right:24px;max-width:1448px;margin-left:auto;margin-right:auto;box-sizing:border-box}
</style>";

    // ── Utility ──────────────────────────────────────────────────────────────

    private static string HtmlEncode(string s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string TruncateStr(string s, int maxLen) =>
        s?.Length > maxLen ? s[..maxLen] + "…" : s ?? "";
}

// ── Data models ──────────────────────────────────────────────────────────────

public record PriceLevel(string Label, string Price, string Type);
public record StockSetup(string Ticker, string Company, string Direction, string Thesis, string KeyLevels, string Notes);
public record MomentumPlay(string Ticker, string Company, string Direction, string BreakoutLevel, string Notes);
public record BigMoneyTrade(string Ticker, string Company, string TradeType, string Structure,
    string CapitalAtRisk, string MaxProfit, string RiskReward, string StopLoss, string Commentary);
