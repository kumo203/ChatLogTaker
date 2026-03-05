using ChatLogTaker.Auth;
using ChatLogTaker.Export;
using ChatLogTaker.Models;
using ChatLogTaker.Teams;
using Microsoft.Playwright;

// ── Parse arguments ───────────────────────────────────────────────────────────
var opts = CliOptions.Parse(args);
if (opts is null) return;

// ── Playwright ────────────────────────────────────────────────────────────────
using var playwright = await Playwright.CreateAsync();
await using var session = new SessionManager();

if (opts.Login || !session.HasSavedSession)
{
    Console.WriteLine("Starting login flow...");
    await session.LoginAndSaveAsync(playwright);
}

Console.WriteLine("Loading saved session...");
await using var context = await session.LoadSessionAsync(playwright, opts.Headed);
var page = await context.NewPageAsync();

Console.WriteLine("Navigating to Microsoft Teams...");
await page.GotoAsync("https://teams.microsoft.com");
await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
await page.WaitForTimeoutAsync(opts.Debug ? 60_000 : 3_000);

// If the session was stale, Teams will redirect to the login page.
// Delete the bad state and re-run with --login.
if (page.Url.Contains("login.microsoftonline.com"))
{
    File.Delete("auth/state.json");
    Console.WriteLine("Saved session has expired. Please re-run with --login to authenticate.");
    return;
}

// --debug: save screenshot + full page HTML to debug/ so we can identify selectors
if (opts.Debug)
{
    Directory.CreateDirectory("debug");
    await page.ScreenshotAsync(new() { Path = "debug/teams.png", FullPage = true });
    await File.WriteAllTextAsync("debug/teams.html", await page.ContentAsync());
    Console.WriteLine("Debug files saved to debug/teams.png and debug/teams.html");
    return;
}

var navigator = new TeamsNavigator(page);
var extractor  = new MessageExtractor(page);
var exporter   = new JsonExporter(opts.OutputDir);

// ── Group chats ───────────────────────────────────────────────────────────────
Console.WriteLine("\n[Group Chats]");
var groupChats = await navigator.GetGroupChatsAsync();

foreach (var chat in groupChats)
{
    Console.WriteLine($"  → {chat.Name}");
    await navigator.NavigateToGroupChatAsync(chat);
    var messages = await extractor.ExtractAllAsync(opts.Limit);

    await exporter.ExportAsync(new ChatLog
    {
        Type        = "GroupChat",
        Name        = chat.Name,
        CollectedAt = DateTime.UtcNow.ToString("O"),
        Messages    = messages,
    });
}

// ── Channels ──────────────────────────────────────────────────────────────────
Console.WriteLine("\n[Channels]");
var channels = await navigator.GetChannelsAsync();

foreach (var ch in channels)
{
    Console.WriteLine($"  → {ch.TeamName} / {ch.ChannelName}");
    await navigator.NavigateToChannelAsync(ch);
    var messages = await extractor.ExtractAllAsync(opts.Limit);

    await exporter.ExportAsync(new ChatLog
    {
        Type        = "Channel",
        Name        = ch.ChannelName,
        TeamName    = ch.TeamName,
        CollectedAt = DateTime.UtcNow.ToString("O"),
        Messages    = messages,
    });
}

Console.WriteLine($"\nDone. Output written to: {Path.GetFullPath(opts.OutputDir)}");

// ── CLI options ───────────────────────────────────────────────────────────────
record CliOptions(bool Login, bool Headed, bool Debug, string OutputDir, int Limit)
{
    public static CliOptions? Parse(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("""
                ChatLogTaker — Microsoft Teams chat log collector

                Usage: ChatLogTaker [options]

                Options:
                  --login           Force re-authentication (ignore saved session)
                  --headed          Show browser window during collection
                  --output <dir>    Output directory  (default: ./output)
                  --limit <n>       Max messages per chat, 0 = all  (default: 0)
                  --debug           Save screenshot + HTML to debug/ then exit
                  --help            Show this help
                """);
            return null;
        }

        bool login  = args.Contains("--login");
        bool headed = args.Contains("--headed") || args.Contains("--debug");
        bool debug  = args.Contains("--debug");

        var outputDir = "output";
        var outIdx = Array.IndexOf(args, "--output");
        if (outIdx >= 0 && outIdx + 1 < args.Length)
            outputDir = args[outIdx + 1];

        var limit = 0;
        var limIdx = Array.IndexOf(args, "--limit");
        if (limIdx >= 0 && limIdx + 1 < args.Length)
            int.TryParse(args[limIdx + 1], out limit);

        return new CliOptions(login, headed, debug, outputDir, limit);
    }
}
