using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace StockedUpAutomation;

/// <summary>
/// Sends the PDF report as an email attachment via Gmail SMTP.
/// Uses System.Net.Mail — built into .NET, no extra packages needed.
/// </summary>
public class EmailService
{
    private readonly GmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(GmailSettings settings, ILogger<EmailService> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    public async Task SendReportAsync(string pdfPath, string dashboardPath, string videoTitle)
    {
        var today   = DateTime.Today.ToString("MMMM dd, yyyy");
        var subject = $"📈 Stocked Up Market Report — {today}";
        var dashFileUri = new Uri(dashboardPath).AbsoluteUri;

        var bodyHtml = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 620px; margin: 0 auto;">
              <div style="background-color: #1A3C5E; padding: 20px 24px; border-radius: 8px 8px 0 0;">
                <h1 style="color: white; margin: 0; font-size: 20px;">📈 Stocked Up</h1>
                <p style="color: #9bb8d4; margin: 4px 0 0; font-size: 13px;">Daily Market Analyst Report</p>
              </div>
              <div style="background: white; border: 1px solid #e2e8f0; border-top: none; padding: 24px; border-radius: 0 0 8px 8px;">
                <p style="margin: 0 0 6px;"><strong>Date:</strong> {today}</p>
                <p style="margin: 0 0 18px;"><strong>Source:</strong> {videoTitle}</p>

                <a href="{dashFileUri}"
                   style="display:inline-block;background:#1A3C5E;color:white;padding:10px 22px;
                          border-radius:8px;text-decoration:none;font-weight:500;font-size:14px;margin-bottom:18px;">
                  Open Trading Dashboard →
                </a>

                <p style="font-size:13px;color:#888;margin:0 0 16px;">
                  (Link opens your local dashboard file. The PDF report is also attached below.)
                </p>

                <hr style="border:none;border-top:1px solid #f0f0f0;margin:16px 0"/>

                <p style="margin: 0 0 8px; font-size: 13px;"><strong>Today's report covers:</strong></p>
                <ul style="font-size: 13px; color: #555; padding-left: 18px; margin: 0 0 16px;">
                  <li>Market Events &amp; Macro Overview</li>
                  <li>Market Sentiment</li>
                  <li>SPY Technical Levels</li>
                  <li>Featured Stock Setups</li>
                  <li>Momentum Plays</li>
                  <li>Big Money Trade of the Day</li>
                  <li>Daily Summary &amp; Outlook</li>
                </ul>

                <p style="font-size: 11px; color: #bbb; margin: 0;">
                  Automated report for informational purposes only. Not financial advice.
                </p>
              </div>
            </body>
            </html>
            """;

        using var message = new MailMessage
        {
            From       = new MailAddress(_settings.Address, "Stocked Up Automation"),
            Subject    = subject,
            Body       = bodyHtml,
            IsBodyHtml = true,
        };
        message.To.Add(_settings.RecipientEmail);
        message.Attachments.Add(new Attachment(pdfPath, "application/pdf"));

        // Attach the PDF
        var attachment = new Attachment(pdfPath, "application/pdf");
        message.Attachments.Add(attachment);

        // Send via Gmail SMTP (port 587 with STARTTLS)
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials    = new NetworkCredential(_settings.Address, _settings.AppPassword),
            EnableSsl      = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        await smtp.SendMailAsync(message);
        _logger.LogInformation("Email sent to {Recipient}", _settings.RecipientEmail);
    }
}
