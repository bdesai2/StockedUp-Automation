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
    private readonly AnthropicClient _client;

    private const string AnalystPrompt = """
        You are a professional stock market analyst. You have been given a transcript from the 
        Stocked Up daily market video. Extract and organize all relevant financial information
        into a structured daily analyst report using the EXACT section format below.

        Use plain text only — no markdown, no asterisks, no pound signs, no dashes as bullets.
        Use the word BULLET on its own line to indicate a bullet point item.
        Use the word TICKER_WATCH followed by comma-separated tickers to flag related stocks.

        ---

        TLDR
        Write 4-5 sentences summarizing the overall market outlook for today. Be direct and
        concise. Include the key macro driver, overall bias, biggest opportunity, and key risk.

        SECTION 1: MARKET EVENTS AND MACRO OVERVIEW
        For each major topic discussed, use this exact format:

        TOPIC: [Short topic title]
        BULLET [one concise bullet — key fact or data point]
        BULLET [one concise bullet — key fact or data point]
        BULLET [one concise bullet — key fact or data point]
        TICKER_WATCH [TICK1, TICK2]

        Limit each topic to 2-4 bullets. Topics might include: geopolitical events, oil prices,
        Fed/inflation data, earnings, strategic reserve releases, supply chain, etc.
        Always end Section 1 with an EARNINGS CALENDAR topic listing any earnings mentioned.

        SECTION 2: MARKET SENTIMENT
        SENTIMENT_OVERALL: [one phrase — e.g. Bearish / Cautious]
        BULLET [Hedge fund positioning fact]
        BULLET [Options/skew data point]
        BULLET [Retail behavior or withdrawal data]
        BULLET [Short squeeze potential or catalyst note]
        BULLET [Any other sentiment indicator cited]

        SECTION 3: SPY TECHNICAL LEVELS
        For each level use this exact pipe-delimited format — one level per line:
        LEVEL_ROW: [price or range] | [Support / Resistance / Target Up / Target Down] | [condition or note]

        Include all levels discussed: key resistance, upside targets, support, downside targets.

        SECTION 4: FEATURED STOCK SETUPS
        For each stock use this exact format:

        TICKER: [symbol]
        COMPANY: [full name]
        DIRECTION: [Bullish or Bearish]
        THESIS: [1-2 sentences max — why this trade makes sense right now]
        KEY LEVELS: [entry trigger | target | stop loss — pipe delimited]
        NOTES: [1 sentence — short float, catalyst, risk, etc.]

        SECTION 5: MOMENTUM PLAYS
        For each play use this exact pipe-delimited format — one play per line:
        MOMENTUM_ROW: [TICKER] | [Company] | [Bullish/Bearish] | [breakout level] | [brief note]
        
        SECTION 6: BIG MONEY TRADE
        TICKER: [symbol]
        COMPANY: [full name]
        TRADE TYPE: [e.g. Call Debit Spread]
        STRUCTURE: [strikes and expiration]
        CAPITAL AT RISK: [dollar amount]
        MAX PROFIT: [dollar amount]
        RISK/REWARD: [ratio]
        STOP LOSS: [level]
        ANALYST COMMENTARY: [2-3 sentences on why this trade makes sense, competitive edge, key risk]

        SECTION 7: DAILY SUMMARY AND OUTLOOK
        BIAS: [Bullish / Bearish / Neutral]
        BULLET [Key takeaway 1]
        BULLET [Key takeaway 2]
        BULLET [Upcoming catalyst]
        BULLET [Sector to watch]
        BULLET [Key risk to monitor]

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

        var response   = await client.Messages.GetClaudeMessageAsync(request);
        var reportText = response.Content.OfType<TextContent>().FirstOrDefault()?.Text

            ?? throw new InvalidOperationException("Claude returned an empty response.");

        _logger.LogInformation("Report generated ({CharCount} characters)", reportText.Length);
        return reportText;
    }
}
