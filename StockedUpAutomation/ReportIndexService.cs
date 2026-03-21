using Microsoft.Extensions.Logging;
using System.Text;

namespace StockedUpAutomation;

/// <summary>
/// Maintains a master index.html in the output directory that lists all
/// past report dashboards by date. Updated on every run.
/// </summary>
public class ReportIndexService
{
    private readonly ILogger<ReportIndexService> _logger;

    public ReportIndexService(ILogger<ReportIndexService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans the output directory for all dated subfolders and
    /// regenerates index.html with a chronological list of all reports.
    /// </summary>
    public void RebuildIndex(string outputDir)
    {
        _logger.LogInformation("Rebuilding report index...");

        var entries = ScanReports(outputDir);
        var html    = BuildIndexHtml(entries);
        var path    = Path.Combine(outputDir, "index.html");

        File.WriteAllText(path, html, Encoding.UTF8);
        _logger.LogInformation("Index updated: {Path} ({Count} reports)", path, entries.Count);
    }

    // ── Scanner ──────────────────────────────────────────────────────────────

    private static List<ReportEntry> ScanReports(string outputDir)
    {
        var entries = new List<ReportEntry>();

        foreach (var folder in Directory.EnumerateDirectories(outputDir))
        {
            var folderName = Path.GetFileName(folder);

            // Only process folders named YYYY-MM-DD
            if (!DateTime.TryParseExact(folderName, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
                continue;

            var dashboardFile = Path.Combine(folder, $"dashboard_{folderName}.html");
            var pdfFile       = Path.Combine(folder, $"StockedUp_Report_{folderName}.pdf");

            entries.Add(new ReportEntry(
                Date:          date,
                DateSlug:      folderName,
                HasDashboard:  File.Exists(dashboardFile),
                HasPdf:        File.Exists(pdfFile),
                DashboardPath: $"./{folderName}/dashboard_{folderName}.html",
                PdfPath:       $"./{folderName}/StockedUp_Report_{folderName}.pdf"
            ));
        }

        // Most recent first
        return entries.OrderByDescending(e => e.Date).ToList();
    }

    // ── HTML builder ─────────────────────────────────────────────────────────

    private static string BuildIndexHtml(List<ReportEntry> entries)
    {
        var updatedAt = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");
        var sb        = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'><head><meta charset='UTF-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine("<title>Stocked Up — Report Archive</title>");
        sb.AppendLine(GetIndexStyles());
        sb.AppendLine("</head><body>");

        // Header
        sb.AppendLine($@"
<div class='header'>
  <div class='header-inner'>
    <div>
      <h1>📈 Stocked Up Report Archive</h1>
      <p class='subtitle'>Daily market analyst reports · Updated {updatedAt}</p>
    </div>
    <div class='count-badge'>{entries.Count} report{(entries.Count != 1 ? "s" : "")}</div>
  </div>
</div>
<div class='container'>");

        if (entries.Count == 0)
        {
            sb.AppendLine("<div class='empty'>No reports found yet. Run the automation to generate your first report.</div>");
        }
        else
        {
            // Latest report hero card
            var latest = entries.First();
            sb.AppendLine($@"
<div class='hero-card'>
  <div class='hero-label'>Latest report</div>
  <div class='hero-date'>{latest.Date:dddd, MMMM dd, yyyy}</div>
  <div class='hero-actions'>
    {(latest.HasDashboard ? $"<a href='{latest.DashboardPath}' class='btn btn-primary'>View Dashboard →</a>" : "")}
    {(latest.HasPdf ? $"<a href='{latest.PdfPath}' class='btn btn-secondary' download>Download PDF</a>" : "")}
  </div>
</div>");

            // Archive table
            sb.AppendLine(@"
<div class='archive-header'>
  <span>All Reports</span>
  <span class='archive-hint'>Click a row to open the dashboard</span>
</div>
<div class='report-list'>");

            foreach (var entry in entries)
            {
                var dayOfWeek  = entry.Date.ToString("ddd");
                var dateLabel  = entry.Date.ToString("MMMM dd, yyyy");
                var isToday    = entry.Date.Date == DateTime.Today;
                var isThisWeek = (DateTime.Today - entry.Date.Date).TotalDays < 7;

                sb.AppendLine($@"
<div class='report-row{(isToday ? " today" : "")}' onclick=""window.location='{entry.DashboardPath}'"">
  <div class='report-row-left'>
    <div class='day-badge{(isToday ? " day-today" : isThisWeek ? " day-week" : "")}'>{dayOfWeek}</div>
    <div>
      <div class='report-date'>{dateLabel}{(isToday ? " <span class='today-pill'>Today</span>" : "")}</div>
      <div class='report-slug'>{entry.DateSlug}</div>
    </div>
  </div>
  <div class='report-row-right'>
    {(entry.HasDashboard ? $"<a href='{entry.DashboardPath}' class='row-btn' onclick='event.stopPropagation()'>Dashboard</a>" : "<span class='missing'>No dashboard</span>")}
    {(entry.HasPdf ? $"<a href='{entry.PdfPath}' class='row-btn row-btn-pdf' download onclick='event.stopPropagation()'>PDF ↓</a>" : "<span class='missing'>No PDF</span>")}
  </div>
</div>");
            }

            sb.AppendLine("</div>"); // report-list
        }

        sb.AppendLine("</div>"); // container
        sb.AppendLine("<div class='footer'>Stocked Up Automation · Reports are for informational purposes only · Not financial advice</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    private static string GetIndexStyles() => @"
<style>
  *{box-sizing:border-box;margin:0;padding:0}
  body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f4f5f7;color:#1a1a2e;font-size:14px}
  .header{background:#1A3C5E;padding:24px;color:white}
  .header-inner{max-width:800px;margin:0 auto;display:flex;align-items:center;justify-content:space-between}
  h1{font-size:22px;font-weight:600;margin-bottom:4px}
  .subtitle{font-size:13px;opacity:.7}
  .count-badge{background:rgba(255,255,255,.15);padding:6px 16px;border-radius:20px;font-size:13px;font-weight:500}
  .container{max-width:800px;margin:24px auto;padding:0 16px}
  .hero-card{background:white;border:1px solid #e2e8f0;border-radius:12px;padding:24px;margin-bottom:20px}
  .hero-label{font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.06em;color:#888;margin-bottom:6px}
  .hero-date{font-size:20px;font-weight:600;color:#1A3C5E;margin-bottom:16px}
  .hero-actions{display:flex;gap:10px}
  .btn{padding:8px 20px;border-radius:8px;font-size:14px;font-weight:500;text-decoration:none;display:inline-block}
  .btn-primary{background:#1A3C5E;color:white}
  .btn-primary:hover{background:#2E5F8A}
  .btn-secondary{background:white;color:#1A3C5E;border:1px solid #d0dce9}
  .btn-secondary:hover{background:#EAF0F7}
  .archive-header{display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;padding:0 4px}
  .archive-header span:first-child{font-size:13px;font-weight:600;color:#444}
  .archive-hint{font-size:12px;color:#aaa}
  .report-list{display:flex;flex-direction:column;gap:6px}
  .report-row{background:white;border:1px solid #e2e8f0;border-radius:10px;padding:14px 16px;display:flex;align-items:center;justify-content:space-between;cursor:pointer;transition:border-color .15s}
  .report-row:hover{border-color:#2E5F8A;background:#fafcff}
  .report-row.today{border-color:#1A3C5E;border-width:2px}
  .report-row-left{display:flex;align-items:center;gap:12px}
  .day-badge{width:40px;height:40px;border-radius:8px;background:#f0f0f0;color:#666;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:600;flex-shrink:0}
  .day-today{background:#1A3C5E;color:white}
  .day-week{background:#EAF0F7;color:#2E5F8A}
  .report-date{font-size:14px;font-weight:500;color:#1a1a2e}
  .report-slug{font-size:12px;color:#aaa;margin-top:2px}
  .today-pill{background:#EAF3DE;color:#3B6D11;font-size:11px;padding:2px 8px;border-radius:20px;font-weight:500;margin-left:6px}
  .report-row-right{display:flex;gap:6px;align-items:center}
  .row-btn{font-size:12px;padding:5px 12px;border-radius:6px;border:1px solid #d0dce9;background:white;color:#2E5F8A;text-decoration:none;font-weight:500}
  .row-btn:hover{background:#EAF0F7}
  .row-btn-pdf{color:#888}
  .missing{font-size:12px;color:#ccc}
  .empty{text-align:center;padding:60px 20px;color:#aaa;font-size:15px}
  .footer{text-align:center;font-size:11px;color:#aaa;padding:32px 16px}
</style>";

    private record ReportEntry(
        DateTime Date,
        string DateSlug,
        bool HasDashboard,
        bool HasPdf,
        string DashboardPath,
        string PdfPath
    );
}
