using ChatLogTaker.Models;
using Microsoft.Playwright;

namespace ChatLogTaker.Teams;

public class MessageExtractor(IPage page)
{
    // The scrollable message thread container.
    // Adjust this selector if Teams changes its markup.
    public const string MessageContainerSelector = "[data-tid='message-list']";

    // Individual rendered message rows
    private const string MessageRowSelector = "[data-tid='messageBodyContent']";
    private const string SenderSelector     = "[data-tid='message-author-name']";
    private const string TimestampSelector  = "time[datetime]";

    private readonly IPage _page = page;

    /// <summary>
    /// Scrolls the message list to the top to load history, then extracts all messages.
    /// </summary>
    /// <param name="limit">Max messages to return (0 = no limit).</param>
    public async Task<List<Message>> ExtractAllAsync(int limit = 0)
    {
        await ScrollToTopAsync();
        return await ParseMessagesAsync(limit);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task ScrollToTopAsync()
    {
        var container = _page.Locator(MessageContainerSelector).First;
        var previousHeight = -1L;
        var stableCount = 0;

        Console.Write("    Loading message history");
        while (stableCount < 3)
        {
            // Scroll to top of the container
            await _page.EvaluateAsync(@"
                const el = document.querySelector('[data-tid=""message-list""]');
                if (el) el.scrollTop = 0;
            ");

            await _page.WaitForTimeoutAsync(1_500);

            // Check if new content was loaded (scroll height increased)
            var currentHeight = await _page.EvaluateAsync<long>(@"
                const el = document.querySelector('[data-tid=""message-list""]');
                return el ? el.scrollHeight : 0;
            ");

            if (currentHeight == previousHeight)
                stableCount++;
            else
                stableCount = 0;

            previousHeight = currentHeight;
            Console.Write(".");
        }

        Console.WriteLine(" done.");
    }

    private async Task<List<Message>> ParseMessagesAsync(int limit)
    {
        var rows = await _page.QuerySelectorAllAsync(MessageRowSelector);
        var messages = new List<Message>();
        var lastSender = "";
        var lastTimestamp = "";

        foreach (var row in rows)
        {
            // Sender and timestamp may only appear on the first message in a group
            var senderEl = await row.QuerySelectorAsync(SenderSelector);
            if (senderEl is not null)
                lastSender = (await senderEl.InnerTextAsync()).Trim();

            var timeEl = await row.QuerySelectorAsync(TimestampSelector);
            if (timeEl is not null)
                lastTimestamp = await timeEl.GetAttributeAsync("datetime") ?? "";

            var body = (await row.InnerTextAsync()).Trim();
            if (string.IsNullOrWhiteSpace(body)) continue;

            messages.Add(new Message
            {
                Sender    = lastSender,
                Timestamp = lastTimestamp,
                Body      = body,
            });

            if (limit > 0 && messages.Count >= limit) break;
        }

        Console.WriteLine($"    Extracted {messages.Count} message(s).");
        return messages;
    }
}
