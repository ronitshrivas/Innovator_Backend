using Innovator.Shared.Entities;

namespace SearchService.Entities;

public class UserIndex : BaseEntity
{
    public Guid AuthUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public string Role { get; set; } = "innovator";
    public string InterestsJson { get; set; } = "[]";
    public int FollowersCount { get; set; } = 0;
    public int FollowingCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class PostIndex : BaseEntity
{
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "post";
    public string HashtagsJson { get; set; } = "[]";
    public string CategoriesJson { get; set; } = "[]";
    public int ReactionsCount { get; set; } = 0;
    public int CommentsCount { get; set; } = 0;
    public int ViewsCount { get; set; } = 0;
    public bool IsReel { get; set; } = false;
}

public class FollowGraph : BaseEntity
{
    public Guid FollowerId { get; set; }
    public Guid FollowingId { get; set; }
}

public class SearchHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string SearchType { get; set; } = "user";
}
