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

    public ReactionService(FeedDbContext db) => _db = db;

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

        return ApiResponse<ReactionResponse>.Ok(
            new ReactionResponse(
                reaction.Id.ToString(),
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

        return ApiResponse<List<ReactionResponse>>.Ok(
            reactions.Select(r => new ReactionResponse(
                r.Id.ToString(),
                r.PostId.ToString(),
                r.Type,
                r.CreatedAt)).ToList());
    }
}
