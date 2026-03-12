using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace StockedUpAutomation;

/// <summary>
/// Sends the video transcript to Claude and returns a structured analyst report.
/// </summary>
public class ReportGeneratorService
{
    private readonly AnthropicSettings _settings;
    private readonly ILogger<ReportGeneratorService> _logger;

    private const string AnalystPrompt = """
        You are a professional stock market analyst. You have been given a transcript from the 
        Stocked Up daily market video. Your job is to extract and organize all relevant financial 
        information into a structured daily analyst report.

        Return your analysis using EXACTLY the following section headers and format.
        Use plain text only — no markdown, no asterisks, no pound signs.

        ---
        REPORT DATE: [today's date]
        VIDEO TITLE: [video title if mentioned]

        SECTION 1: MARKET EVENTS AND MACRO OVERVIEW
        Summarize all major geopolitical, economic, and macro events discussed. Include specific 
        data points, prices, percentages, and named entities.

        SECTION 2: MARKET SENTIMENT
        Describe overall market sentiment. Include hedge fund positioning, retail behavior, 
        options skew, fear/greed indicators, and any sentiment data cited.

        SECTION 3: SPY TECHNICAL LEVELS
        List all support, resistance, and key price levels discussed for SPY/S&P 500. 
        Format each level as: LEVEL | DIRECTION | CONDITION/NOTE

        SECTION 4: FEATURED STOCK SETUPS
        For each stock mentioned as a setup, provide:
        TICKER: [symbol]
        COMPANY: [name]
        DIRECTION: [Bullish/Bearish]
        THESIS: [why]
        KEY LEVELS: [entry triggers, targets, stop losses]
        NOTES: [any extra context]

        SECTION 5: MOMENTUM PLAYS
        List momentum/continuation stocks. For each:
        TICKER | COMPANY | DIRECTION | BREAKOUT LEVEL | NOTES

        SECTION 6: BIG MONEY TRADE
        Detail the institutional or large options trade discussed:
        TICKER: [symbol]
        COMPANY: [name]
        TRADE TYPE: [call spread, put, etc.]
        STRUCTURE: [strikes, expiration]
        CAPITAL AT RISK: [dollar amount]
        MAX PROFIT: [dollar amount]
        RISK/REWARD: [ratio]
        ANALYST COMMENTARY: [why this trade makes sense]

        SECTION 7: DAILY SUMMARY AND OUTLOOK
        Provide a concise summary of the day's key takeaways, upcoming catalysts, 
        sectors to watch, and overall directional bias.
        ---

        Here is the transcript:

        {transcript}
        """;

    public ReportGeneratorService(AnthropicSettings settings, ILogger<ReportGeneratorService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Sends the transcript to Claude and returns the structured report as plain text.
    /// </summary>
    public async Task<string> GenerateReportAsync(string transcript, string videoTitle)
    {
        _logger.LogInformation("Sending transcript to Claude API...");

        var client = new AnthropicClient(_settings.ApiKey);

        var prompt = AnalystPrompt.Replace("{transcript}", transcript);

        var request = new MessageParameters
        {
            Model    = AnthropicModels.Claude46Sonnet,
            MaxTokens = 4000,
            Messages = new List<Message>
            {
                new Message(RoleType.User, prompt)
            }
        };

        var response = await client.Messages.GetClaudeMessageAsync(request);
        var reportText = response.Content.OfType<TextContent>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Claude returned an empty response.");

        _logger.LogInformation("Report generated successfully ({CharCount} characters)", reportText.Length);
        return reportText;
    }
}
