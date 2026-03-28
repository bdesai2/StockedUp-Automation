# StockedUp Automation — AI Coding Guidelines

## Architecture Overview
**.NET 8 console application** that automates daily stock market report generation. Pipeline: YouTube video transcript → Anthropic Claude AI analysis → PDF report (QuestPDF) + HTML dashboard (TradingView charts) → Gmail email delivery.

Designed to run via **Windows Task Scheduler at 6 PM weekdays** (report is for next trading day's market open).

## Pipeline Flow (Program.cs)
Linear 8-step orchestrator with early exits:
1. **Trading day check** — `TradingCalendar.IsTradingDay()` checks if *tomorrow* is a trading day (NYSE holidays through 2030, weekends)
2. **Fetch latest video** — YouTube Data API v3 from configured channel
3. **Duplicate check** — `VideoTrackingService` reads `last_exported_video.json` to skip already-processed videos
4. **Fetch transcript** — Runs **Python subprocess** (`Python/get_transcript.py`) using `youtube-transcript-api`
5. **AI analysis** — `ReportGeneratorService` sends transcript to Claude with structured prompt
6. **Build PDF** — `PdfBuilderService` (QuestPDF) renders branded multi-section report
7. **Build HTML dashboard** — `DashboardBuilderService` generates self-contained HTML with TradingView chart iframes
8. **Email + open** — `EmailService` sends PDF attachment + dashboard link via Gmail SMTP

## Key Services
| Service | File | Purpose |
|---|---|---|
| `YouTubeService` | `YouTubeService.cs` | YouTube Data API search + Python sidecar for transcript |
| `TradingCalendar` | `TradingCalendar.cs` | Static utility — NYSE holidays, weekend detection, Computus for Good Friday |
| `ReportGeneratorService` | `ReportGeneratorService.cs` | Claude API call with structured prompt; returns text with markers (`TOPIC:`, `BULLET`, `TICKER_WATCH`, `LEVEL_ROW:`, `MOMENTUM_ROW:`) |
| `PdfBuilderService` | `PdfBuilderService.cs` | QuestPDF fluent builder (706 lines) — 7 section-specific renderers |
| `DashboardBuilderService` | `DashboardBuilderService.cs` | Self-contained HTML + CSS + TradingView embeds (509 lines) |
| `EmailService` | `EmailService.cs` | Gmail SMTP via `System.Net.Mail.SmtpClient` |
| `ReportIndexService` | `ReportIndexService.cs` | Rebuilds master `index.html` archive of all reports |

## Configuration
- `appsettings.json` — runtime config (API keys, paths, email settings)
- `AppSettings.cs` — strongly-typed POCOs bound via `ConfigurationBuilder` (no DI container)
- `appsettings.example.json` — template showing required keys
- **No dependency injection** — all services are `new`'d in `Program.cs` with manual logger wiring

## Build & Run
```bash
dotnet build
dotnet run --project StockedUpAutomation
dotnet publish -c Release -r win-x64 --self-contained   # For Task Scheduler deployment
```
Or open `StockedUpAutomation.sln` in Visual Studio 2022 and F5.

## External Dependencies
- **YouTube Data API v3** — `Google.Apis.YouTube.v3` NuGet
- **Python runtime** — `Python/get_transcript.py` requires `youtube-transcript-api` pip package (runtime dependency not managed by .NET)
- **Anthropic Claude** — `Anthropic` v5.10.0 NuGet SDK
- **QuestPDF** — Community license (declared in `PdfBuilderService` constructor)
- **TradingView** — Public widget iframes in dashboard HTML (no API key)

## Conventions
- **Namespace:** `StockedUpAutomation` (flat — no sub-namespaces)
- **Service naming:** `{Feature}Service` pattern
- **DTOs:** C# records for immutable data types
- **Logging:** `Microsoft.Extensions.Logging` with `ILogger<T>`, emoji prefixes in console output (`✓`, `✗`)
- **HTML generation:** Inline string building with `StringBuilder`, no templating engine
- **Output:** Date-stamped folders in configured `Output.Directory` — `StockedUp_Report_{yyyy-MM-dd}.pdf` + `dashboard.html`

## Known Issues / Gotchas
1. **Python sidecar required** — `Python/get_transcript.py` must be deployed alongside the exe; Python + `youtube-transcript-api` must be installed
2. **Report text parsing is fragile** — Both `PdfBuilderService` and `DashboardBuilderService` independently parse Claude's output via line-by-line string matching with markers. If Claude deviates from expected format, sections silently fail. Parsing logic is duplicated.
3. **No retry logic** — All API calls (YouTube, Claude, SMTP) are fire-once with no retry or timeout configuration
4. **Claude model hardcoded** — Uses `Anthropic.Models.Claude35Sonnet` constant, not configurable
5. **Step numbering comments in Program.cs are wrong** — duplicate step numbers; actual flow doesn't match comment labels
6. **`.bak` files exist** — Backup copies of `EmailService.cs`, `PdfBuilderService.cs`, `Program.cs` — ignore these
