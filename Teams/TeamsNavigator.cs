using Microsoft.Playwright;

namespace ChatLogTaker.Teams;

public record GroupChatInfo(string Name, string? Href);
public record ChannelInfo(string TeamName, string ChannelName, string? Href);

public class TeamsNavigator(IPage page)
{
    // ── Left-nav selectors ────────────────────────────────────────────────────
    // These use aria-label which is more stable than class names.
    // Adjust if Teams changes its markup.
    private const string ChatNavSelector   = "[aria-label='Chat']";
    private const string TeamsNavSelector  = "[aria-label='Teams']";

    // ── Chat list ─────────────────────────────────────────────────────────────
    // Each item in the chat list. Group chats have a participant count badge
    // or a comma-separated name. We collect all and let the caller filter.
    private const string ChatItemSelector  = "[data-tid='chat-list-item']";
    private const string ChatNameSelector  = "[data-tid='chat-title']";

    // ── Teams / channel list ──────────────────────────────────────────────────
    private const string TeamItemSelector    = "[data-tid='team-channel-list-team']";
    private const string TeamNameSelector    = "[data-tid='team-channel-list-team-title']";
    private const string ChannelItemSelector = "[data-tid='team-channel-list-channel']";
    private const string ChannelNameSelector = "[data-tid='channel-title']";

    private readonly IPage _page = page;

    /// <summary>
    /// Enumerates all group chats visible in the Chat tab.
    /// A "group chat" is any chat with more than 2 participants (name contains comma or ·).
    /// </summary>
    public async Task<List<GroupChatInfo>> GetGroupChatsAsync()
    {
        await ClickNavAsync(ChatNavSelector, "Chat");

        // Wait for the chat list to populate
        await _page.WaitForSelectorAsync(ChatItemSelector, new() { Timeout = 15_000 });

        var items = await _page.QuerySelectorAllAsync(ChatItemSelector);
        var chats = new List<GroupChatInfo>();

        foreach (var item in items)
        {
            var nameEl = await item.QuerySelectorAsync(ChatNameSelector);
            var name = nameEl is not null ? (await nameEl.InnerTextAsync()).Trim() : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Group chats typically have comma-separated names or a participant count.
            // DMs are single-person names. We keep entries that look like group chats.
            // Adjust this heuristic if needed.
            if (!IsGroupChat(name)) continue;

            var href = await item.GetAttributeAsync("href");
            chats.Add(new GroupChatInfo(name, href));
        }

        Console.WriteLine($"  Found {chats.Count} group chat(s).");
        return chats;
    }

    /// <summary>
    /// Enumerates all channels across all teams the user is a member of.
    /// </summary>
    public async Task<List<ChannelInfo>> GetChannelsAsync()
    {
        await ClickNavAsync(TeamsNavSelector, "Teams");

        await _page.WaitForSelectorAsync(TeamItemSelector, new() { Timeout = 15_000 });

        var teamItems = await _page.QuerySelectorAllAsync(TeamItemSelector);
        var channels = new List<ChannelInfo>();

        foreach (var teamItem in teamItems)
        {
            var teamNameEl = await teamItem.QuerySelectorAsync(TeamNameSelector);
            var teamName = teamNameEl is not null ? (await teamNameEl.InnerTextAsync()).Trim() : "Unknown Team";

            // Expand team if collapsed
            var expanded = await teamItem.GetAttributeAsync("aria-expanded");
            if (expanded == "false")
                await teamItem.ClickAsync();

            await _page.WaitForTimeoutAsync(500);

            var channelItems = await teamItem.QuerySelectorAllAsync(ChannelItemSelector);
            foreach (var ch in channelItems)
            {
                var chNameEl = await ch.QuerySelectorAsync(ChannelNameSelector);
                var chName = chNameEl is not null ? (await chNameEl.InnerTextAsync()).Trim() : "";
                if (string.IsNullOrWhiteSpace(chName)) continue;

                var href = await ch.GetAttributeAsync("href");
                channels.Add(new ChannelInfo(teamName, chName, href));
            }
        }

        Console.WriteLine($"  Found {channels.Count} channel(s) across {teamItems.Count} team(s).");
        return channels;
    }

    /// <summary>Navigates to a group chat and waits for messages to load.</summary>
    public async Task NavigateToGroupChatAsync(GroupChatInfo chat)
    {
        if (chat.Href is not null)
        {
            await _page.GotoAsync(chat.Href);
        }
        else
        {
            // Fallback: click the item in the list
            await ClickNavAsync(ChatNavSelector, "Chat");
            var nameEl = _page.Locator($"{ChatItemSelector}:has-text('{EscapeSelector(chat.Name)}')");
            await nameEl.First.ClickAsync();
        }

        await WaitForMessagesAsync();
    }

    /// <summary>Navigates to a channel and waits for messages to load.</summary>
    public async Task NavigateToChannelAsync(ChannelInfo channel)
    {
        if (channel.Href is not null)
        {
            await _page.GotoAsync(channel.Href);
        }
        else
        {
            await ClickNavAsync(TeamsNavSelector, "Teams");
            var chEl = _page.Locator(
                $"{ChannelItemSelector}:has-text('{EscapeSelector(channel.ChannelName)}')");
            await chEl.First.ClickAsync();
        }

        await WaitForMessagesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ClickNavAsync(string selector, string name)
    {
        Console.WriteLine($"  Navigating to {name} tab...");
        var el = _page.Locator(selector).First;
        await el.WaitForAsync(new() { Timeout = 10_000 });
        await el.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private async Task WaitForMessagesAsync()
    {
        // Wait for the message thread area to appear
        await _page.WaitForSelectorAsync(
            MessageExtractor.MessageContainerSelector,
            new() { Timeout = 20_000 });
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private static bool IsGroupChat(string name) =>
        name.Contains(',') || name.Contains('·') || name.Contains(" and ");

    private static string EscapeSelector(string value) =>
        value.Replace("'", "\\'");
}
