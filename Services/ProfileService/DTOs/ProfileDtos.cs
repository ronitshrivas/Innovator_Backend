using System.ComponentModel.DataAnnotations;

namespace ProfileService.DTOs;

public record ProfileLink(string Label, string Url);

public record UpdateProfileRequest(
    string? FullName,
    string? Bio,
    string? DateOfBirth,
    string? Phone,
    string? Gender,
    string? Address,
    string? Education,
    string? Occupation,
    List<string>? Interests,
    // Multi-value professional detail (like LinkedIn). Each is optional; when
    // provided it replaces the stored list.
    List<string>? Educations,
    List<string>? Occupations,
    List<ProfileLink>? Links
);

public record ProfileResponse(
    Guid Id,
    Guid AuthUserId,
    string Username,
    string FullName,
    string Email,
    string Role,
    string? Bio,
    string? Avatar,
    string? CoverImage,
    string? DateOfBirth,
    string? Phone,
    string? Gender,
    string? Address,
    string? Education,
    string? Occupation,
    List<string> Interests,
    List<string> Educations,
    List<string> Occupations,
    List<ProfileLink> Links,
    int FollowersCount,
    int FollowingCount,
    bool IsFollowed,
    DateTime CreatedAt
);

public record CoverImageResponse(string CoverImage);

public record SuggestedUserDto(
    Guid Id,
    string Username,
    string FullName,
    string? Avatar,
    string? Occupation,
    int MutualCount,
    string Reason
);

public record UserSummaryDto(
    Guid Id,
    string Username,
    string FullName,
    string? Avatar,
    string Role,
    string? Occupation,
    bool IsFollowed
);

// A person shown in the "Find Friends" discovery list. Headline is the user's
// occupation, or their education when no occupation is set (LinkedIn-style).
public record FindFriendDto(
    Guid Id,
    string Username,
    string FullName,
    string? Avatar,
    string? Headline,
    bool IsFollowed,
    string FollowStatus // none | pending | accepted
);

public record FindFriendsPageDto(
    IReadOnlyList<FindFriendDto> People,
    int Page,
    int PageSize,
    bool HasMore
);

public record FollowActionResponse(
    bool IsFollowing,
    string Message,
    string Status = "none" // none | pending | accepted
);

public record BlockActionResponse(
    bool IsBlocked,
    string Message
);

public record BlockedUserDto(
    Guid Id,
    string Username,
    string FullName,
    string? Avatar
);
