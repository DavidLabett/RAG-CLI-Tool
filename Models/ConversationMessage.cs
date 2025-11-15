namespace SecondBrain.Models;

/// <summary>
/// Represents a message in a conversation (either user question or assistant answer)
/// </summary>
public class ConversationMessage
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ConversationMessage(string role, string content)
    {
        Role = role;
        Content = content;
        Timestamp = DateTime.UtcNow;
    }
}

