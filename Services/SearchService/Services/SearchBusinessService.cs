using System.Text.Json;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using SearchService.Data;
using SearchService.DTOs;
using SearchService.Entities;

namespace SearchService.Services;

public interface ISearchService
{
    Task<ApiResponse<SearchResultDto>> SearchAsync(string query, Guid requesterId, string type);
    Task<ApiResponse<List<SearchUserDto>>> SearchUsersAsync(string query, Guid requesterId);
    Task<ApiResponse<List<SearchPostDto>>> SearchPostsAsync(string query);
    Task<ApiResponse<List<string>>> SearchHashtagsAsync(string query);
    Task<ApiResponse<SuggestionResponse>> GetSuggestedUsersAsync(Guid userId);
    Task<ApiResponse<List<SearchHistoryDto>>> GetSearchHistoryAsync(Guid userId);
    Task<ApiResponse<bool>> ClearSearchHistoryAsync(Guid userId);
}

public class SearchBusinessService : ISearchService
{
    private readonly SearchDbContext _db;
    private readonly IConfiguration _config;

    public SearchBusinessService(SearchDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ApiResponse<SearchResultDto>> SearchAsync(
        string query, Guid requesterId, string type)
    {
        query = query.Trim();

        if (!string.IsNullOrEmpty(query))
            await SaveSearchHistoryAsync(requesterId, query, type);

        var users = new List<SearchUserDto>();
        var posts = new List<SearchPostDto>();
        var hashtags = new List<string>();

        if (type == "all" || type == "users")
        {
            var userResult = await SearchUsersAsync(query, requesterId);
            users = userResult.Data ?? new();
        }

        if (type == "all" || type == "posts")
        {
            var postResult = await SearchPostsAsync(query);
            posts = postResult.Data ?? new();
        }

        if (type == "all" || type == "hashtags")
        {
            var hashResult = await SearchHashtagsAsync(query);
            hashtags = hashResult.Data ?? new();
        }

        return ApiResponse<SearchResultDto>.Ok(new SearchResultDto(
            users, posts, hashtags, users.Count, posts.Count));
    }

    public async Task<ApiResponse<List<SearchUserDto>>> SearchUsersAsync(
        string query, Guid requesterId)
    {
        var q = query.ToLower().Trim();

        var users = await _db.UserIndex
            .Where(u => u.IsActive &&
                        u.AuthUserId != requesterId &&
                        (u.Username.ToLower().Contains(q) ||
                         u.FullName.ToLower().Contains(q)))
            .OrderByDescending(u => u.FollowersCount)
            .Take(20)
            .ToListAsync();

        var baseUrl = _config["PublicBaseUrl"] ?? string.Empty;

        return ApiResponse<List<SearchUserDto>>.Ok(
            users.Select(u => new SearchUserDto(
                u.AuthUserId.ToString(),
                u.Username,
                ResolveUrl(u.Avatar, baseUrl)
            )).ToList());
    }

    public async Task<ApiResponse<List<SearchPostDto>>> SearchPostsAsync(string query)
    {
        var q = query.ToLower().Trim();

        var posts = await _db.PostIndex
            .Where(p => p.Content.ToLower().Contains(q) ||
                        p.HashtagsJson.ToLower().Contains(q))
            .OrderByDescending(p => p.ReactionsCount + p.ViewsCount)
            .Take(20)
            .ToListAsync();

        var baseUrl = _config["PublicBaseUrl"] ?? string.Empty;

        return ApiResponse<List<SearchPostDto>>.Ok(
            posts.Select(p => new SearchPostDto(
                p.PostId.ToString(),
                p.AuthorId.ToString(),
                p.Username,
                ResolveUrl(p.Avatar, baseUrl),
                p.Content,
                p.Type,
                JsonSerializer.Deserialize<List<string>>(p.HashtagsJson) ?? new(),
                JsonSerializer.Deserialize<List<string>>(p.CategoriesJson) ?? new(),
                p.ReactionsCount,
                p.CommentsCount,
                p.ViewsCount,
                p.CreatedAt
            )).ToList());
    }

    public async Task<ApiResponse<List<string>>> SearchHashtagsAsync(string query)
    {
        var q = query.ToLower().Replace("#", "").Trim();

        var posts = await _db.PostIndex
            .Where(p => p.HashtagsJson.ToLower().Contains(q))
            .Select(p => p.HashtagsJson)
            .Take(100)
            .ToListAsync();

        var hashtags = posts
            .SelectMany(h => JsonSerializer.Deserialize<List<string>>(h) ?? new())
            .Where(h => h.ToLower().Contains(q))
            .GroupBy(h => h.ToLower())
            .OrderByDescending(g => g.Count())
            .Select(g => g.First())
            .Take(20)
            .ToList();

        return ApiResponse<List<string>>.Ok(hashtags);
    }

    public async Task<ApiResponse<SuggestionResponse>> GetSuggestedUsersAsync(Guid userId)
    {
        var myFollowing = await _db.FollowGraph
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowingId)
            .ToListAsync();

        var followsMe = await _db.FollowGraph
            .Where(f => f.FollowingId == userId)
            .Select(f => f.FollowerId)
            .ToListAsync();

        var excludeIds = myFollowing.Concat(new[] { userId }).ToHashSet();

        var myUser = await _db.UserIndex.FirstOrDefaultAsync(u => u.AuthUserId == userId);
        var myInterests = myUser != null
            ? JsonSerializer.Deserialize<List<string>>(myUser.InterestsJson) ?? new()
            : new List<string>();

        var candidates = await _db.UserIndex
            .Where(u => u.IsActive && !excludeIds.Contains(u.AuthUserId))
            .OrderByDescending(u => u.FollowersCount)
            .Take(50)
            .ToListAsync();

        var baseUrl = _config["PublicBaseUrl"] ?? string.Empty;

        var suggestions = new List<SuggestedUserDto>();

        foreach (var candidate in candidates)
        {
            var theirFollowing = await _db.FollowGraph
                .Where(f => f.FollowerId == candidate.AuthUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            var mutualCount = theirFollowing.Intersect(myFollowing).Count();
            var followsBack = followsMe.Contains(candidate.AuthUserId);

            var theirInterests = JsonSerializer
                .Deserialize<List<string>>(candidate.InterestsJson) ?? new();
            var sharedTags = myInterests.Intersect(theirInterests,
                StringComparer.OrdinalIgnoreCase).ToList();

            var affinityScore = (mutualCount * 3) +
                                (followsBack ? 5 : 0) +
                                (sharedTags.Count * 2) +
                                (candidate.FollowersCount / 100);

            var reason = followsBack ? "Follows you back"
                : mutualCount > 0 ? $"{mutualCount} mutual connections"
                : sharedTags.Any() ? $"Interested in {sharedTags.First()}"
                : "Popular innovator";

            suggestions.Add(new SuggestedUserDto(
                UserId: candidate.AuthUserId.ToString(),
                Username: candidate.Username,
                FullName: candidate.FullName,
                Avatar: ResolveUrl(candidate.Avatar, baseUrl),
                Bio: candidate.Bio,
                IsFollowing: false,
                FollowsMe: followsBack,
                MutualCount: mutualCount,
                AffinityScore: affinityScore,
                SharedTags: sharedTags,
                Reason: reason
            ));
        }

        var sorted = suggestions
            .OrderByDescending(s => s.AffinityScore)
            .Take(20)
            .ToList();

        return ApiResponse<SuggestionResponse>.Ok(
            new SuggestionResponse(sorted.Count, sorted));
    }

    public async Task<ApiResponse<List<SearchHistoryDto>>> GetSearchHistoryAsync(Guid userId)
    {
        var history = await _db.SearchHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(20)
            .ToListAsync();

        return ApiResponse<List<SearchHistoryDto>>.Ok(
            history.Select(h => new SearchHistoryDto(
                h.Id.ToString(),
                h.Query,
                h.SearchType,
                h.CreatedAt)).ToList());
    }

    public async Task<ApiResponse<bool>> ClearSearchHistoryAsync(Guid userId)
    {
        await _db.SearchHistory
            .Where(h => h.UserId == userId)
            .ExecuteDeleteAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private async Task SaveSearchHistoryAsync(Guid userId, string query, string type)
    {
        var existing = await _db.SearchHistory
            .FirstOrDefaultAsync(h => h.UserId == userId &&
                                      h.Query.ToLower() == query.ToLower());

        if (existing != null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.SearchHistory.Add(new SearchHistory
            {
                UserId = userId,
                Query = query,
                SearchType = type
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string? ResolveUrl(string? path, string baseUrl)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith("http")) return path;
        return $"{baseUrl}{path}";
    }
}
