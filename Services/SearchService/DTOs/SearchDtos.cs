using System.ComponentModel.DataAnnotations;

namespace SearchService.DTOs;

public record SearchUserDto(
    string Id,
    string Username,
    string? Avatar
);

public record SuggestedUserDto(
    string UserId,
    string Username,
    string FullName,
    string? Avatar,
    string? Bio,
    bool IsFollowing,
    bool FollowsMe,
    int MutualCount,
    int AffinityScore,
    List<string> SharedTags,
    string Reason
);

public record SuggestionResponse(
    int Total,
    List<SuggestedUserDto> Suggestions
);

public record SearchPostDto(
    string Id,
    string AuthorId,
    string Username,
    string? Avatar,
    string Content,
    string Type,
    List<string> Hashtags,
    List<string> Categories,
    int ReactionsCount,
    int CommentsCount,
    int ViewsCount,
    DateTime CreatedAt
);

public record SearchResultDto(
    List<SearchUserDto> Users,
    List<SearchPostDto> Posts,
    List<string> Hashtags,
    int TotalUsers,
    int TotalPosts
);

public record UpsertUserIndexRequest(
    Guid AuthUserId,
    string Username,
    string FullName,
    string? Avatar,
    string? Bio,
    string Role,
    List<string>? Interests,
    int FollowersCount,
    int FollowingCount,
    bool ShowInSearch = true
);

public record UpsertPostIndexRequest(
    Guid PostId,
    Guid AuthorId,
    string Username,
    string? Avatar,
    string Content,
    string Type,
    List<string>? Hashtags,
    List<string>? Categories,
    int ReactionsCount,
    int CommentsCount,
    int ViewsCount,
    bool IsReel
);

public record SyncFollowRequest(
    Guid FollowerId,
    Guid FollowingId,
    bool IsFollowing
);

public record SearchHistoryDto(
    string Id,
    string Query,
    string SearchType,
    DateTime CreatedAt
);
