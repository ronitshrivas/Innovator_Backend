namespace ProfileService.DTOs;

/// Full settings snapshot returned to the owner.
public record SettingsResponse(
    bool PushEnabled,
    bool NotifyLikes,
    bool NotifyComments,
    bool NotifyFollows,
    bool NotifyMentions,
    bool NotifyMessages,
    bool NotifyReposts,
    bool EmailDigest,
    bool PrivateAccount,
    string WhoCanMessage,
    string WhoCanComment,
    bool ShowActivityStatus,
    bool ShowInSearch,
    string Language,
    string Theme,
    string? Timezone
);

/// Partial update — every field is nullable; only provided fields are applied.
public record UpdateSettingsRequest(
    bool? PushEnabled,
    bool? NotifyLikes,
    bool? NotifyComments,
    bool? NotifyFollows,
    bool? NotifyMentions,
    bool? NotifyMessages,
    bool? NotifyReposts,
    bool? EmailDigest,
    bool? PrivateAccount,
    string? WhoCanMessage,
    string? WhoCanComment,
    bool? ShowActivityStatus,
    bool? ShowInSearch,
    string? Language,
    string? Theme,
    string? Timezone
);

/// Compact per-user flags other services read to enforce rules (batch lookup).
public record SettingsFlags(
    string UserId,
    bool PushEnabled,
    bool NotifyLikes,
    bool NotifyComments,
    bool NotifyFollows,
    bool NotifyMentions,
    bool NotifyMessages,
    bool NotifyReposts,
    bool PrivateAccount,
    string WhoCanMessage,
    string WhoCanComment,
    bool ShowInSearch
);

public record SettingsFlagsRequest(List<string>? UserIds);
