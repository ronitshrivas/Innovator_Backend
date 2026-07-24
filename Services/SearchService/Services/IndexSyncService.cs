using System.Text.Json;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using SearchService.Data;
using SearchService.DTOs;
using SearchService.Entities;

namespace SearchService.Services;

public interface IIndexSyncService
{
    Task<ApiResponse<bool>> UpsertUserAsync(UpsertUserIndexRequest request);
    Task<ApiResponse<bool>> UpsertPostAsync(UpsertPostIndexRequest request);
    Task<ApiResponse<bool>> DeletePostAsync(Guid postId);
    Task<ApiResponse<bool>> DeleteUserAsync(Guid authUserId);
    Task<ApiResponse<bool>> SyncFollowAsync(SyncFollowRequest request);
}

public class IndexSyncService : IIndexSyncService
{
    private readonly SearchDbContext _db;

    public IndexSyncService(SearchDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> UpsertUserAsync(UpsertUserIndexRequest request)
    {
        var existing = await _db.UserIndex
            .FirstOrDefaultAsync(u => u.AuthUserId == request.AuthUserId);

        if (existing != null)
        {
            existing.Username = request.Username;
            existing.FullName = request.FullName;
            existing.Avatar = request.Avatar;
            existing.Bio = request.Bio;
            existing.Role = request.Role;
            existing.InterestsJson = JsonSerializer.Serialize(request.Interests ?? new());
            existing.FollowersCount = request.FollowersCount;
            existing.FollowingCount = request.FollowingCount;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserIndex.Add(new UserIndex
            {
                AuthUserId = request.AuthUserId,
                Username = request.Username,
                FullName = request.FullName,
                Avatar = request.Avatar,
                Bio = request.Bio,
                Role = request.Role,
                InterestsJson = JsonSerializer.Serialize(request.Interests ?? new()),
                FollowersCount = request.FollowersCount,
                FollowingCount = request.FollowingCount
            });
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> UpsertPostAsync(UpsertPostIndexRequest request)
    {
        var existing = await _db.PostIndex
            .FirstOrDefaultAsync(p => p.PostId == request.PostId);

        var hashtags = ExtractHashtags(request.Content);
        var allHashtags = hashtags
            .Concat(request.Hashtags ?? new())
            .Distinct()
            .ToList();

        if (existing != null)
        {
            existing.Content = request.Content;
            existing.HashtagsJson = JsonSerializer.Serialize(allHashtags);
            existing.CategoriesJson = JsonSerializer.Serialize(request.Categories ?? new());
            existing.ReactionsCount = request.ReactionsCount;
            existing.CommentsCount = request.CommentsCount;
            existing.ViewsCount = request.ViewsCount;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.PostIndex.Add(new PostIndex
            {
                PostId = request.PostId,
                AuthorId = request.AuthorId,
                Username = request.Username,
                Avatar = request.Avatar,
                Content = request.Content,
                Type = request.Type,
                HashtagsJson = JsonSerializer.Serialize(allHashtags),
                CategoriesJson = JsonSerializer.Serialize(request.Categories ?? new()),
                ReactionsCount = request.ReactionsCount,
                CommentsCount = request.CommentsCount,
                ViewsCount = request.ViewsCount,
                IsReel = request.IsReel
            });
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeletePostAsync(Guid postId)
    {
        await _db.PostIndex
            .Where(p => p.PostId == postId)
            .ExecuteDeleteAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid authUserId)
    {
        await _db.UserIndex
            .Where(u => u.AuthUserId == authUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> SyncFollowAsync(SyncFollowRequest request)
    {
        var existing = await _db.FollowGraph
            .FirstOrDefaultAsync(f =>
                f.FollowerId == request.FollowerId &&
                f.FollowingId == request.FollowingId);

        if (request.IsFollowing)
        {
            if (existing == null)
            {
                _db.FollowGraph.Add(new FollowGraph
                {
                    FollowerId = request.FollowerId,
                    FollowingId = request.FollowingId
                });
            }
        }
        else
        {
            if (existing != null)
                _db.FollowGraph.Remove(existing);
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    private static List<string> ExtractHashtags(string content)
    {
        if (string.IsNullOrEmpty(content)) return new();

        return content.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.StartsWith('#') && w.Length > 1)
            .Select(w => w.TrimEnd('.', ',', '!', '?').ToLower())
            .Distinct()
            .ToList();
    }
}
