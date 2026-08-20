using FeedService.Data;
using FeedService.DTOs;
using FeedService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

public interface IReactionService
{
    Task<ApiResponse<ReactionResponse>> ToggleReactionAsync(Guid postId, Guid userId, string type);
    Task<ApiResponse<List<ReactionResponse>>> GetReactionsAsync(Guid postId);
}

public class ReactionService : IReactionService
{
    private readonly FeedDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IProfileAvatarResolver _authors;

    public ReactionService(
        FeedDbContext db,
        INotificationService notifications,
        IProfileAvatarResolver authors)
    {
        _db = db;
        _notifications = notifications;
        _authors = authors;
    }

    public async Task<ApiResponse<ReactionResponse>> ToggleReactionAsync(
        Guid postId, Guid userId, string type)
    {
        var post = await _db.Posts.FindAsync(postId);
        if (post == null) return ApiResponse<ReactionResponse>.Fail("Post not found.");

        var existing = await _db.Reactions
            .FirstOrDefaultAsync(r => r.PostId == postId && r.AuthorId == userId);

        if (existing != null)
        {
            if (existing.Type == type)
            {
                _db.Reactions.Remove(existing);
                await _db.SaveChangesAsync();
                return ApiResponse<ReactionResponse>.Ok(null!, "Reaction removed.");
            }

            existing.Type = type;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return ApiResponse<ReactionResponse>.Ok(
                new ReactionResponse(
                    existing.Id.ToString(),
                    userId.ToString(),
                    string.Empty,
                    null,
                    postId.ToString(),
                    existing.Type,
                    existing.CreatedAt));
        }

        var reaction = new Reaction
        {
            PostId = postId,
            AuthorId = userId,
            Type = type
        };

        _db.Reactions.Add(reaction);
        await _db.SaveChangesAsync();

        // Resolve the actor's real username + avatar from the profile service
        // (authoritative — works even if the actor has never posted).
        var actorInfo = await _authors.ResolveAuthorsAsync(new[] { userId }, null);
        actorInfo.TryGetValue(userId.ToString(), out var info);

        var fallback = await _db.Posts
            .Where(p => p.AuthorId == userId)
            .Select(p => new { p.Username, p.Avatar })
            .FirstOrDefaultAsync();

        var actorName = info?.Username ?? fallback?.Username ?? "Someone";
        var actorAvatar = info?.Avatar ?? fallback?.Avatar;

        // Notify the post's author that someone reacted.
        await _notifications.CreateAsync(new CreateNotificationRequest(
            UserId: post.AuthorId,
            Title: actorName,
            Message: $"{actorName} reacted to your post.",
            Type: "like",
            SenderId: userId,
            SenderUsername: actorName,
            SenderAvatar: actorAvatar,
            RelatedPostId: postId));

        return ApiResponse<ReactionResponse>.Ok(
            new ReactionResponse(
                reaction.Id.ToString(),
                userId.ToString(),
                actorName,
                actorAvatar,
                postId.ToString(),
                reaction.Type,
                reaction.CreatedAt));
    }

    public async Task<ApiResponse<List<ReactionResponse>>> GetReactionsAsync(Guid postId)
    {
        var reactions = await _db.Reactions
            .Where(r => r.PostId == postId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Resolve each reactor's current username + avatar from the profile service.
        var authorIds = reactions.Select(r => r.AuthorId);
        var authors = await _authors.ResolveAuthorsAsync(authorIds, null);

        return ApiResponse<List<ReactionResponse>>.Ok(
            reactions.Select(r =>
            {
                authors.TryGetValue(r.AuthorId.ToString(), out var info);
                return new ReactionResponse(
                    r.Id.ToString(),
                    r.AuthorId.ToString(),
                    info?.Username ?? string.Empty,
                    info?.Avatar,
                    r.PostId.ToString(),
                    r.Type,
                    r.CreatedAt);
            }).ToList());
    }
}
