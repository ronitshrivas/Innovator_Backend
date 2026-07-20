using System.ComponentModel.DataAnnotations;

namespace ChatService.DTOs;

public record CreateConversationRequest(
    [Required] Guid ParticipantUserId,
    string ParticipantUsername,
    string? ParticipantAvatar
);

public record SendMessageRequest(
    [Required, MinLength(1), MaxLength(5000)] string Content,
    string MessageType = "text",
    string? MediaUrl = null,
    string? ReplyToId = null
);

public record UpdateMessageRequest(
    [Required, MinLength(1)] string Content
);

public record ConversationResponse(
    string Id,
    string Type,
    List<ParticipantDto> Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTime CreatedAt
);

public record ParticipantDto(
    string UserId,
    string Username,
    string? Avatar,
    DateTime? LastReadAt
);

public record MessageDto(
    string Id,
    string ConversationId,
    string SenderId,
    string SenderUsername,
    string? SenderAvatar,
    string Content,
    string MessageType,
    string? MediaUrl,
    bool IsRead,
    bool IsDeleted,
    string? ReplyToId,
    MessageDto? ReplyTo,
    DateTime CreatedAt
);
