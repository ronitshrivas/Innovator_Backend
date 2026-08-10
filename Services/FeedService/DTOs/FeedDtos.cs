using System.ComponentModel.DataAnnotations;

namespace FeedService.DTOs;

public record CreatePostRequest(
    [Required, MinLength(1), MaxLength(5000)] string Content,
    List<string>? CategoryIds,
    string? SharedPostId
);

public record UpdatePostRequest(
    [Required, MinLength(1)] string Content
);

public record CreateReactionRequest(
    [Required] string Post,
    [Required] string Type
);



public record CreateCommentRequest(
    string? Post,
    string? Reel,
    [Required, MinLength(1), MaxLength(2000)] string Content
);

public record CreateReplyRequest(
    [Required] string Parent,
    [Required, MinLength(1), MaxLength(2000)] string Content
);

public record UpdateCommentRequest(
    [Required, MinLength(1)] string Content
);

public record PostMediaDto(
    string Id,
    string File,
    string MediaType,
    string? Thumbnail
);

public record CategoryDto(
    string Id,
    string Name,
    string? Description
);

public record PostResponse(
    string Id,
    string UserId,
    string Username,
    string Avatar,
    string? Occupation,
    string Content,
    string Type,
    bool IsReel,
    List<PostMediaDto> Media,
    List<CategoryDto> CategoriesDetail,
    int ReactionsCount,
    int CommentsCount,
    int ShareCount,
    int ViewsCount,
    string? CurrentUserReaction,
    bool IsFollowed,
    string? SharedPost,
    SharedPostDetailsDto? SharedPostDetails,
    DateTime CreatedAt
);

public record SharedPostDetailsDto(
    string Id,
    string UserId,
    string Username,
    string FullName,
    string? Avatar,
    string Content,
    DateTime CreatedAt,
    List<PostMediaDto> Media
);

public record ReactionResponse(
    string Id,
    string UserId,
    string Username,
    string? Avatar,
    string Post,
    string Type,
    DateTime CreatedAt
);

public record CommentResponse(
    string Id,
    string UserId,
    string Username,
    string? Avatar,
    string Post,
    string? Parent,
    string Content,
    int ReplyCount,
    DateTime CreatedAt
);

public record FeedResponse(
    List<PostResponse> Results,
    int Count,
    string? Next,
    string? Previous
);

// Shape matches the app's AppNotification.fromJson (snake_case keys):
// id, title, message, type, sender_username, sender_avatar, sender,
// related_post_id, created_at, is_read.
public record NotificationDto(
    string Id,
    string Title,
    string Message,
    string Type,
    string? SenderUsername,
    string? SenderAvatar,
    string? Sender,
    string? RelatedPostId,
    string CreatedAt,
    bool IsRead
);

public record FcmTokenRequest(string Token, string? DeviceName);

public record FcmTokenResponse(string Id, string Token, string? DeviceName);

// Called internally by the feed/profile flows to raise a notification.
public record CreateNotificationRequest(
    Guid UserId,
    string Title,
    string Message,
    string Type,
    Guid? SenderId,
    string? SenderUsername,
    string? SenderAvatar,
    Guid? RelatedPostId
);
