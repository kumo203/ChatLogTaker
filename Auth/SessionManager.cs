using Microsoft.Playwright;

namespace ChatLogTaker.Auth;

public class SessionManager : IAsyncDisposable
{
    private const string StateFile = "auth/state.json";
    private IBrowser? _browser;

    public bool HasSavedSession => File.Exists(StateFile);

    /// <summary>
    /// Opens a headed browser for the user to log in, then saves the session state.
    /// </summary>
    public async Task LoginAndSaveAsync(IPlaywright playwright)
    {
        Directory.CreateDirectory("auth");

        _browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://teams.microsoft.com");

        Console.WriteLine("=== Login Required ===");
        Console.WriteLine("Complete your login in the browser window that just opened.");
        Console.WriteLine("Once the Teams interface is fully loaded, press Enter here to save the session.");
        Console.ReadLine();

        await context.StorageStateAsync(new() { Path = StateFile });
        Console.WriteLine($"Session saved to: {StateFile}");

        await _browser.CloseAsync();
        _browser = null;
    }

    /// <summary>
    /// Loads a previously saved session into a new browser context.
    /// </summary>
    public async Task<IBrowserContext> LoadSessionAsync(IPlaywright playwright, bool headed = false)
    {
        _browser = await playwright.Chromium.LaunchAsync(new() { Headless = !headed });
        return await _browser.NewContextAsync(new() { StorageStatePath = StateFile });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
    }
}
