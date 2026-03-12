# StockedUp Daily Report Automation — C# Setup Guide

Complete step-by-step instructions for Visual Studio 2022 on Windows.
Estimated setup time: 30–45 minutes.

---

## Prerequisites

- Visual Studio 2022 (any edition — Community is free)
- .NET 8 SDK (included with VS 2022, or download from https://dot.net)
- A Google account (YouTube API + Gmail)
- An Anthropic account (Claude API)

---

## PART 1 — Get Your API Keys

### A) YouTube Data API Key (Free)

1. Go to https://console.cloud.google.com
2. Click "Select a project" → "New Project"
   - Name: StockedUp Automation → Click Create
3. Left menu → APIs & Services → Library
4. Search "YouTube Data API v3" → Click it → Enable
5. Left menu → APIs & Services → Credentials
6. Click "+ Create Credentials" → "API Key"
7. Copy the key — you'll paste it into appsettings.json

### B) Stocked Up Channel ID

1. Go to https://commentpicker.com/youtube-channel-id.php
2. Paste the Stocked Up YouTube channel URL
3. Copy the Channel ID (starts with "UC...")
4. Paste into appsettings.json as ChannelId

### C) Anthropic API Key

1. Go to https://console.anthropic.com
2. Sign up or log in → API Keys → Create Key
3. Copy the key — paste into appsettings.json
4. Add a small credit balance ($5 lasts months at ~$0.03/day)

### D) Gmail App Password

Your regular Gmail password won't work here — you need an App Password:

1. Go to https://myaccount.google.com/security
2. Ensure "2-Step Verification" is ON (required)
3. Search for "App passwords" in the search bar
4. App: Mail | Device: Windows Computer → Generate
5. Copy the 16-character password shown
6. Paste into appsettings.json as AppPassword

---

## PART 2 — Open and Configure the Project

### Step 1: Open the Solution

1. Open Visual Studio 2022
2. File → Open → Project/Solution
3. Browse to your project folder
4. Select: StockedUpAutomation.sln
5. Click Open

### Step 2: Restore NuGet Packages

Visual Studio will automatically restore packages on first open.
If not: right-click the solution in Solution Explorer → Restore NuGet Packages

### Step 3: Fill In appsettings.json

In Solution Explorer, open appsettings.json and fill in your values:

```json
{
  "YouTube": {
    "ApiKey": "AIzaSyXXXXXXXXXXXXXXXXXXXXXXXX",
    "ChannelId": "UCxxxxxxxxxxxxxxxxxxxxxxxxx"
  },
  "Anthropic": {
    "ApiKey": "sk-ant-XXXXXXXXXXXXXXXXXXXXXXXXXX"
  },
  "Gmail": {
    "Address": "yourname@gmail.com",
    "AppPassword": "abcd efgh ijkl mnop",
    "RecipientEmail": "yourname@gmail.com"
  },
  "Output": {
    "Directory": "C:\\Users\\YourName\\Documents\\StockedUpReports"
  }
}
```

IMPORTANT: Create the output directory in Windows Explorer before running,
or the app will create it automatically.

---

## PART 3 — Build and Test

### Step 1: Build the Project

In Visual Studio: Build → Build Solution (Ctrl+Shift+B)

You should see: "Build succeeded" in the output window.
If there are errors, check that NuGet packages restored correctly.

### Step 2: Run It Manually (First Test)

Press F5 (or Debug → Start Debugging) to run it now.

You should see a console window with output like:
```
18:00:01  INFO  StockedUp Automation — Starting
18:00:02  INFO  ✓ Trading day confirmed.
18:00:04  INFO  ✓ Video: [latest video title]
18:00:07  INFO  ✓ Transcript fetched (12483 characters)
18:00:19  INFO  ✓ Report generated.
18:00:20  INFO  ✓ PDF saved to: C:\...\StockedUp_Report_2026-03-12.pdf
18:00:21  INFO  ✓ Email sent to: you@gmail.com
18:00:21  INFO  StockedUp Automation — Complete ✓
```

Check your inbox — the PDF should arrive within a minute!

### Step 3: Publish a Release Build

Once testing works, publish a self-contained .exe for scheduling:

1. Right-click the project in Solution Explorer → Publish
2. Click "Folder" → Next → Next → Finish
3. Click "Show all settings":
   - Configuration: Release
   - Target framework: net8.0
   - Deployment mode: Self-contained
   - Target runtime: win-x64
4. Click Publish
5. Note the output folder path (e.g. bin\Release\net8.0\win-x64\publish\)
6. The .exe file there is: StockedUpAutomation.exe

---

## PART 4 — Schedule with Windows Task Scheduler

Now schedule the .exe to run automatically at 6:00 PM on trading days.

Note: The app checks the trading calendar internally, so you can schedule it
for every weekday — it will silently exit on holidays.

### Step 1: Open Task Scheduler

Press Windows key → search "Task Scheduler" → Open

### Step 2: Create a New Task

Click "Create Task" (NOT "Create Basic Task") in the right panel.

#### General tab:
- Name: StockedUp Market Report
- Description: Generates and emails daily stock market analyst report
- Check: "Run whether user is logged on or not"
- Check: "Run with highest privileges"

#### Triggers tab:
- Click New
- Begin the task: On a schedule
- Settings: Weekly
- Start: set today's date, Time: 6:00:00 PM
- Check: Monday, Tuesday, Wednesday, Thursday, Friday
- Click OK

#### Actions tab:
- Click New
- Action: Start a program
- Program/script: Browse to your StockedUpAutomation.exe
  Example: C:\Users\YourName\source\repos\StockedUpAutomation\bin\Release\net8.0\win-x64\publish\StockedUpAutomation.exe
- Start in: the folder containing the .exe (same directory, without the filename)
- Click OK

#### Conditions tab:
- Uncheck "Start the task only if the computer is on AC power"
  (so it runs even if you're on battery)

#### Settings tab:
- Check: "Run task as soon as possible after a scheduled start is missed"
  (helpful if your PC was off at 6PM)

### Step 3: Save the Task

Click OK. Enter your Windows password if prompted.

### Step 4: Test It

Right-click your new task in the list → Run
Check your inbox for the report.

---

## PART 5 — Troubleshooting

### Build errors about missing packages
Right-click solution → Restore NuGet Packages → rebuild

### "Could not find transcript" error
The video may not have auto-captions yet (takes 1-2 hours after upload).
Consider changing the scheduled time to 7:00 PM to be safe.

### Gmail authentication error
- Make sure you're using the App Password (16 chars), not your regular password
- Make sure 2-Step Verification is enabled on your Google account

### Task Scheduler runs but nothing happens
- Check automation.log in the same folder as the .exe for error details
- Make sure "Start in" is set correctly in the Task Scheduler action

### "Not a trading day" on a day the market is open
- Extremely rare edge case — the TradingCalendar class covers all NYSE holidays
- If a special market closure is announced (e.g. national day of mourning),
  add it manually to TradingCalendar.cs in GetNyseHolidays()

---

## Project File Reference

| File | Purpose |
|---|---|
| Program.cs | Entry point — orchestrates all 6 steps |
| AppSettings.cs | Strongly-typed config model |
| TradingCalendar.cs | NYSE holiday logic — no NuGet needed |
| YouTubeService.cs | Fetches latest video + transcript |
| ReportGeneratorService.cs | Calls Claude API, returns structured text |
| PdfBuilderService.cs | Builds formatted PDF with QuestPDF |
| EmailService.cs | Sends email with PDF via Gmail SMTP |
| appsettings.json | Your API keys and config — fill this in |
| StockedUpAutomation.csproj | Project file with all NuGet references |
| SETUP.md | This guide |

---

## NuGet Packages Used

| Package | Purpose |
|---|---|
| Google.Apis.YouTube.v3 | Official YouTube Data API client |
| HtmlAgilityPack | HTML parsing for transcript extraction |
| Anthropic.SDK | Claude API client |
| QuestPDF | PDF generation (free, open source) |
| Microsoft.Extensions.Configuration.Json | appsettings.json support |
| Microsoft.Extensions.Logging.Console | Console + file logging |
