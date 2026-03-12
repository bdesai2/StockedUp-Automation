StockedUp-Automation

An automated YouTube video transcript analyzer for stock market commentary.
This .NET 8 console application monitors a specific YouTube channel (StockedUp), fetches transcripts from the latest videos, and generates AI-powered reports using Claude (Anthropic). The system:
•	Extracts video transcripts using YoutubeExplode library with a fallback scraping method
•	Generates market analysis reports by processing transcripts through Anthropic's Claude API
•	Sends email summaries via Gmail to specified recipients
•	Respects trading calendar to run only on market days
•	Logs all operations with structured logging for monitoring and debugging
Tech Stack: .NET 8, Google YouTube Data API v3, YoutubeExplode, Anthropic Claude API, SMTP email delivery
Use Case: Automates the process of consuming financial YouTube content by converting video commentary into structured, readable reports delivered via email.
