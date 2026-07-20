using Innovator.Shared.Entities;

namespace FeedService.Entities;

public class Post : BaseEntity
{
    public Guid AuthorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "post";
    public bool IsReel { get; set; } = false;
    public int ViewsCount { get; set; } = 0;

    public Guid? SharedPostId { get; set; }
    public Post? SharedPost { get; set; }

    public List<PostMedia> Media { get; set; } = new();
    public List<PostCategory> Categories { get; set; } = new();
    public List<Reaction> Reactions { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public List<Post> Reposts { get; set; } = new();
}

public class PostMedia : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public string File { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public string? Thumbnail { get; set; }
}

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<PostCategory> Posts { get; set; } = new();
}

public class PostCategory
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}

public class Reaction : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public string Type { get; set; } = "like";
}

public class Comment : BaseEntity
{
    public Guid? PostId { get; set; }
    public Post? Post { get; set; }
    public Guid AuthorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Comment? Parent { get; set; }
    public List<Comment> Replies { get; set; } = new();
}
