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

    /// <summary>
    /// Sends the report PDF to the configured recipient email address.
    /// </summary>
    public async Task SendReportAsync(string pdfPath, string videoTitle)
    {
        var today   = DateTime.Today.ToString("MMMM dd, yyyy");
        var subject = $"📈 Stocked Up Market Report — {today}";

        var bodyHtml = $"""
            <html>
            <body style="font-family: Arial, sans-serif; color: #333; max-width: 600px;">
              <div style="background-color: #1A3C5E; padding: 16px; text-align: center;">
                <h1 style="color: white; margin: 0; font-size: 20px;">Stocked Up</h1>
                <p style="color: #EAF0F7; margin: 4px 0 0 0; font-size: 13px;">Daily Market Analyst Report</p>
              </div>
              <div style="padding: 20px;">
                <p><strong>Date:</strong> {today}</p>
                <p><strong>Source Video:</strong> {videoTitle}</p>
                <p>Your automated daily analyst report is attached as a PDF.</p>
                <p><strong>This report covers:</strong></p>
                <ul>
                  <li>Market Events &amp; Macro Overview</li>
                  <li>Market Sentiment</li>
                  <li>SPY Technical Levels</li>
                  <li>Featured Stock Setups</li>
                  <li>Momentum Plays</li>
                  <li>Big Money Trade of the Day</li>
                  <li>Daily Summary &amp; Outlook</li>
                </ul>
              </div>
              <div style="background-color: #f5f5f5; padding: 12px; text-align: center;">
                <p style="font-size: 10px; color: #999; margin: 0;">
                  This is an automated report for informational purposes only. Not financial advice.
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
