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

public record UserSummaryDto(
    Guid Id,
    string Username,
    string FullName,
    string? Avatar,
    string Role,
    bool IsFollowed
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
