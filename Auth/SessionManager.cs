using Microsoft.Playwright;

namespace ChatLogTaker.Auth;

public class SessionManager : IAsyncDisposable
{
    private const string StateFile = "auth/state.json";
    private IBrowser? _browser;

    public bool HasSavedSession => File.Exists(StateFile);

    /// <summary>
    /// Opens a headed browser for the user to log in, then auto-saves the session
    /// once Teams finishes loading (no manual Enter press needed).
    /// </summary>
    public async Task LoginAndSaveAsync(IPlaywright playwright)
    {
        Directory.CreateDirectory("auth");

        _browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://teams.microsoft.com");

        Console.WriteLine("=== Login Required ===");
        Console.WriteLine("A browser window has opened. Please log in to Microsoft Teams.");
        Console.WriteLine("The session will be saved automatically once you are logged in...");

        // Strategy: after the initial navigation, Teams immediately redirects to
        // login.microsoftonline.com. We wait for that redirect to occur (up to 10s),
        // then wait for the URL to come back to teams.microsoft.com, which means
        // login completed. We then wait for the network to idle before saving.

        // Step 1: wait for the login redirect to happen
        Console.WriteLine("  Waiting for login page...");
        try
        {
            await page.WaitForURLAsync(
                url => !url.StartsWith("https://teams.microsoft.com"),
                new() { Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
            // Might already be logged in — no redirect occurred, continue
        }

        // Step 2: wait for redirect back to teams.microsoft.com (user completes login)
        Console.WriteLine("  Waiting for you to complete login in the browser...");
        try
        {
            await page.WaitForURLAsync(
                url => url.StartsWith("https://teams.microsoft.com"),
                new() { Timeout = 300_000 });   // 5 minutes
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Timed out waiting for login. Please try --login again.");
            return;
        }

        // Step 3: wait for Teams to finish loading
        Console.WriteLine("  Login detected. Waiting for Teams to load...");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await page.WaitForTimeoutAsync(4_000);

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
