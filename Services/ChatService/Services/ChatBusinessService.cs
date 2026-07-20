using ChatService.Data;
using ChatService.DTOs;
using ChatService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Services;

public interface IChatService
{
    Task<ApiResponse<ConversationResponse>> GetOrCreateConversationAsync(
        Guid requesterId, string requesterUsername, string? requesterAvatar,
        CreateConversationRequest request);
    Task<ApiResponse<List<ConversationResponse>>> GetMyConversationsAsync(Guid userId);
    Task<ApiResponse<ConversationResponse>> GetConversationAsync(Guid conversationId, Guid userId);
    Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(Guid conversationId, Guid userId, int page);
    Task<ApiResponse<MessageDto>> SendMessageAsync(Guid conversationId, Guid senderId,
        string senderUsername, string? senderAvatar, SendMessageRequest request);
    Task<ApiResponse<MessageDto>> UpdateMessageAsync(Guid messageId, Guid requesterId, string content);
    Task<ApiResponse<bool>> DeleteMessageAsync(Guid messageId, Guid requesterId);
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid conversationId, Guid userId);
    Task<ApiResponse<bool>> DeleteConversationAsync(Guid conversationId, Guid userId);
}

public class ChatBusinessService : IChatService
{
    private readonly ChatDbContext _db;

    public ChatBusinessService(ChatDbContext db) => _db = db;

    public async Task<ApiResponse<ConversationResponse>> GetOrCreateConversationAsync(
        Guid requesterId, string requesterUsername, string? requesterAvatar,
        CreateConversationRequest request)
    {
        var existing = await _db.Conversations
            .Include(c => c.Participants)
            .Include(c => c.LastMessage)
            .Where(c => c.Type == "direct" &&
                        c.Participants.Any(p => p.UserId == requesterId) &&
                        c.Participants.Any(p => p.UserId == request.ParticipantUserId))
            .FirstOrDefaultAsync();

        if (existing != null)
            return ApiResponse<ConversationResponse>.Ok(
                await MapToResponseAsync(existing, requesterId));

        var conversation = new Conversation { Type = "direct" };

        conversation.Participants.Add(new ConversationParticipant
        {
            UserId = requesterId,
            Username = requesterUsername,
            Avatar = requesterAvatar,
            ConversationId = conversation.Id
        });

        conversation.Participants.Add(new ConversationParticipant
        {
            UserId = request.ParticipantUserId,
            Username = request.ParticipantUsername,
            Avatar = request.ParticipantAvatar,
            ConversationId = conversation.Id
        });

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        return ApiResponse<ConversationResponse>.Ok(
            await MapToResponseAsync(conversation, requesterId));
    }

    public async Task<ApiResponse<List<ConversationResponse>>> GetMyConversationsAsync(Guid userId)
    {
        var conversations = await _db.Conversations
            .Include(c => c.Participants)
            .Include(c => c.LastMessage)
            .Where(c => c.Participants.Any(p => p.UserId == userId && p.IsActive))
            .OrderByDescending(c => c.LastMessage != null
                ? c.LastMessage.CreatedAt : c.CreatedAt)
            .ToListAsync();

        var results = new List<ConversationResponse>();
        foreach (var c in conversations)
            results.Add(await MapToResponseAsync(c, userId));

        return ApiResponse<List<ConversationResponse>>.Ok(results);
    }

    public async Task<ApiResponse<ConversationResponse>> GetConversationAsync(
        Guid conversationId, Guid userId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
            .Include(c => c.LastMessage)
            .FirstOrDefaultAsync(c => c.Id == conversationId &&
                                      c.Participants.Any(p => p.UserId == userId));

        if (conversation == null)
            return ApiResponse<ConversationResponse>.Fail("Conversation not found.");

        return ApiResponse<ConversationResponse>.Ok(
            await MapToResponseAsync(conversation, userId));
    }

    public async Task<ApiResponse<List<MessageDto>>> GetMessagesAsync(
        Guid conversationId, Guid userId, int page)
    {
        var isMember = await _db.Participants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isMember)
            return ApiResponse<List<MessageDto>>.Fail("Access denied.");

        var skip = (page - 1) * 30;

        var messages = await _db.Messages
            .Include(m => m.ReplyTo)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip).Take(30)
            .ToListAsync();

        return ApiResponse<List<MessageDto>>.Ok(
            messages.Select(MapMessageToDto).ToList());
    }

    public async Task<ApiResponse<MessageDto>> SendMessageAsync(
        Guid conversationId, Guid senderId,
        string senderUsername, string? senderAvatar,
        SendMessageRequest request)
    {
        var isMember = await _db.Participants
            .AnyAsync(p => p.ConversationId == conversationId &&
                           p.UserId == senderId && p.IsActive);

        if (!isMember)
            return ApiResponse<MessageDto>.Fail("Access denied.");

        Guid? replyToId = null;
        if (!string.IsNullOrEmpty(request.ReplyToId) &&
            Guid.TryParse(request.ReplyToId, out var rid))
            replyToId = rid;

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            SenderUsername = senderUsername,
            SenderAvatar = senderAvatar,
            Content = request.Content,
            MessageType = request.MessageType,
            MediaUrl = request.MediaUrl,
            ReplyToId = replyToId
        };

        _db.Messages.Add(message);

        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastMessageId = message.Id;
            conversation.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var saved = await _db.Messages
            .Include(m => m.ReplyTo)
            .FirstAsync(m => m.Id == message.Id);

        return ApiResponse<MessageDto>.Ok(MapMessageToDto(saved));
    }

    public async Task<ApiResponse<MessageDto>> UpdateMessageAsync(
        Guid messageId, Guid requesterId, string content)
    {
        var message = await _db.Messages
            .Include(m => m.ReplyTo)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return ApiResponse<MessageDto>.Fail("Message not found.");

        if (message.SenderId != requesterId)
            return ApiResponse<MessageDto>.Fail("Not authorized.");

        message.Content = content;
        message.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<MessageDto>.Ok(MapMessageToDto(message));
    }

    public async Task<ApiResponse<bool>> DeleteMessageAsync(Guid messageId, Guid requesterId)
    {
        var message = await _db.Messages.FindAsync(messageId);
        if (message == null) return ApiResponse<bool>.Fail("Message not found.");
        if (message.SenderId != requesterId) return ApiResponse<bool>.Fail("Not authorized.");

        message.IsDeleted = true;
        message.Content = "This message was deleted.";
        message.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid conversationId, Guid userId)
    {
        var participant = await _db.Participants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (participant == null)
            return ApiResponse<bool>.Fail("Conversation not found.");

        participant.LastReadAt = DateTime.UtcNow;

        await _db.Messages
            .Where(m => m.ConversationId == conversationId &&
                        m.SenderId != userId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteConversationAsync(Guid conversationId, Guid userId)
    {
        var participant = await _db.Participants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (participant == null)
            return ApiResponse<bool>.Fail("Conversation not found.");

        participant.IsActive = false;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private async Task<ConversationResponse> MapToResponseAsync(Conversation c, Guid userId)
    {
        var unread = await _db.Messages
            .CountAsync(m => m.ConversationId == c.Id &&
                             m.SenderId != userId && !m.IsRead);

        return new ConversationResponse(
            c.Id.ToString(),
            c.Type,
            c.Participants.Select(p => new ParticipantDto(
                p.UserId.ToString(),
                p.Username,
                p.Avatar,
                p.LastReadAt)).ToList(),
            c.LastMessage != null ? MapMessageToDto(c.LastMessage) : null,
            unread,
            c.CreatedAt);
    }

    private static MessageDto MapMessageToDto(Message m) =>
        new(m.Id.ToString(),
            m.ConversationId.ToString(),
            m.SenderId.ToString(),
            m.SenderUsername,
            m.SenderAvatar,
            m.Content,
            m.MessageType,
            m.MediaUrl,
            m.IsRead,
            m.IsDeleted,
            m.ReplyToId?.ToString(),
            m.ReplyTo != null ? MapMessageToDto(m.ReplyTo) : null,
            m.CreatedAt);
}
