using Innovator.Shared.Entities;

namespace ChatService.Entities;

public class Conversation : BaseEntity
{
    public string Type { get; set; } = "direct";
    public List<ConversationParticipant> Participants { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
    public Message? LastMessage { get; set; }
    public Guid? LastMessageId { get; set; }
}

public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public DateTime? LastReadAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public string? MediaUrl { get; set; }
    public bool IsRead { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public Guid? ReplyToId { get; set; }
    public Message? ReplyTo { get; set; }
}
