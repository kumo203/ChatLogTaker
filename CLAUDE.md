# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Environment

- **Shell**: `pwsh` (PowerShell) — use by default. Avoid Python/Node unless there is a specific reason.
- **Platform**: Windows 11
- **.NET**: 10.0 (`net10.0`)

## Project Overview

ChatLogTaker is a C# .NET 10 CLI tool that collects Microsoft Teams chat logs (group chats + team channels) for normal users (no admin rights). It uses **Playwright** browser automation against `https://teams.microsoft.com` instead of the Graph API (which requires admin consent for chat-read scopes).

## Commands

```pwsh
# Scaffold (first time only)
dotnet new console -n ChatLogTaker --framework net10.0

# Install Playwright browsers after first build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium

# Build
dotnet build

# Run — first-time login
dotnet run -- --login

# Run — collect logs
dotnet run -- --output ./output

# Run with options
dotnet run -- --output ./output --limit 500 --headed
```

## Architecture

```
ChatLogTaker/
├── Program.cs                  # Entry point, CLI arg parsing (System.CommandLine)
├── Auth/
│   └── SessionManager.cs       # Save/load Playwright browser state (auth/state.json)
├── Teams/
│   ├── TeamsNavigator.cs       # Enumerate group chats and team channels
│   └── MessageExtractor.cs     # Scroll-to-top loop + parse messages
├── Models/
│   ├── ChatLog.cs              # Root output model
│   └── Message.cs              # Per-message model { sender, timestamp, body }
└── Export/
    └── JsonExporter.cs         # Write ChatLog → JSON file
```

### Authentication flow
- **First run / `--login`**: launches a headed browser, navigates to Teams, waits for the user to complete login, then saves browser state (cookies + localStorage) to `./auth/state.json`.
- **Subsequent runs**: restores state from `./auth/state.json` and runs headless.

### Chat/channel enumeration
- **Group chats**: Click "Chat" tab in left nav → iterate sidebar items with ≥3 participants.
- **Team channels**: Click "Teams" tab → iterate each team → each channel.

### Message extraction
Scroll the message list to the top repeatedly until no new content loads, then parse each bubble for sender, timestamp (`datetime` attribute on `<time>`), and body text.

### Output schema
One JSON file per chat/channel in `./output/`:
```json
{
  "type": "GroupChat | Channel",
  "name": "...",
  "teamName": "... (channels only)",
  "collectedAt": "<ISO 8601>",
  "messages": [
    { "sender": "...", "timestamp": "<ISO 8601>", "body": "..." }
  ]
}
```

## CLI Options

| Flag | Default | Description |
|------|---------|-------------|
| `--login` | off | Force re-authentication |
| `--output <dir>` | `./output` | Output directory |
| `--limit <n>` | `0` (all) | Max messages per chat |
| `--headed` | off | Show browser on non-login runs |

## NuGet Dependencies

- `Microsoft.Playwright`
- `System.CommandLine`
