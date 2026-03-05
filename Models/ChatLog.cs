namespace ChatLogTaker.Models;

public class ChatLog
{
    public string Type { get; set; } = "";       // "GroupChat" | "Channel"
    public string Name { get; set; } = "";
    public string? TeamName { get; set; }        // channels only
    public string CollectedAt { get; set; } = "";
    public List<Message> Messages { get; set; } = [];
}
