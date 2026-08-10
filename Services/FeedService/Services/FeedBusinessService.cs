using FeedService.Data;
using FeedService.DTOs;
using FeedService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

public interface IFeedService
{
    Task<ApiResponse<FeedResponse>> GetFeedAsync(Guid userId, int page, int pageSize);
    Task<ApiResponse<PostResponse>> CreatePostAsync(
        Guid authorId, string username, string avatar,
        CreatePostRequest request, List<IFormFile>? mediaFiles);
    Task<ApiResponse<PostResponse>> GetPostAsync(Guid postId, Guid requesterId);
    Task<ApiResponse<PostResponse>> UpdatePostAsync(Guid postId, Guid requesterId, string content, IFormFile? mediaFile);
    Task<ApiResponse<bool>> DeletePostAsync(Guid postId, Guid requesterId);
    Task<ApiResponse<int>> RecordViewAsync(Guid postId);
    Task<ApiResponse<FeedResponse>> GetReelFeedAsync(Guid userId, int page, int pageSize);
    Task<ApiResponse<PostResponse>> CreateReelAsync(
        Guid authorId, string username, string avatar,
        string caption, IFormFile videoFile);
    Task<ApiResponse<FeedResponse>> GetUserPostsAsync(Guid authorId, Guid requesterId, int page, int pageSize);
    Task<ApiResponse<FeedResponse>> GetRepostsAsync(Guid postId, Guid requesterId, int page, int pageSize);
    Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync();
}

public class FeedBusinessService : IFeedService
{
    private readonly FeedDbContext _db;
    private readonly IMediaStorageService _mediaStorage;
    private readonly IProfileAvatarResolver _avatarResolver;

    public FeedBusinessService(
        FeedDbContext db,
        IMediaStorageService mediaStorage,
        IProfileAvatarResolver avatarResolver)
    {
        _db = db;
        _mediaStorage = mediaStorage;
        _avatarResolver = avatarResolver;
    }

    public async Task<ApiResponse<FeedResponse>> GetFeedAsync(Guid userId, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;

        var query = _db.Posts
            .Where(p => !p.IsReel)
            .Include(p => p.Media)
            .Include(p => p.Categories).ThenInclude(pc => pc.Category)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .Include(p => p.SharedPost).ThenInclude(sp => sp != null ? sp.Media : null)
            .OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync();
        var posts = await query.Skip(skip).Take(pageSize).ToListAsync();

        var results = posts.Select(p => MapToResponse(p, userId)).ToList();

        // Enrich each post (and any shared/original post) with the author's
        // current avatar, occupation and follow state from the profile service.
        results = await EnrichAuthorsAsync(results, posts, userId);

        var hasNext = skip + posts.Count < total;
        var next = hasNext ? $"/api/feed?page={page + 1}&pageSize={pageSize}" : null;
        var previous = page > 1 ? $"/api/feed?page={page - 1}&pageSize={pageSize}" : null;

        return ApiResponse<FeedResponse>.Ok(new FeedResponse(results, total, next, previous));
    }

    public async Task<ApiResponse<PostResponse>> CreatePostAsync(
        Guid authorId, string username, string avatar,
        CreatePostRequest request, List<IFormFile>? mediaFiles)
    {
        Post? sharedPost = null;
        if (!string.IsNullOrEmpty(request.SharedPostId) &&
            Guid.TryParse(request.SharedPostId, out var sharedId))
        {
            sharedPost = await _db.Posts.FindAsync(sharedId);
            if (sharedPost != null)
                sharedPost.ViewsCount++;
        }

        var post = new Post
        {
            AuthorId = authorId,
            Username = username,
            Avatar = avatar,
            Content = request.Content,
            Type = "post",
            IsReel = false,
            SharedPostId = sharedPost?.Id
        };

        if (request.CategoryIds?.Any() == true)
        {
            var catIds = request.CategoryIds
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

            var cats = await _db.Categories.Where(c => catIds.Contains(c.Id)).ToListAsync();
            post.Categories = cats.Select(c => new PostCategory
            {
                CategoryId = c.Id,
                PostId = post.Id
            }).ToList();
        }

        if (mediaFiles?.Any() == true)
        {
            foreach (var file in mediaFiles)
            {
                var (filePath, mediaType, thumbnail) =
                    await _mediaStorage.SaveMediaAsync(file, username);

                post.Media.Add(new PostMedia
                {
                    File = filePath,
                    MediaType = mediaType,
                    Thumbnail = thumbnail,
                    PostId = post.Id
                });
            }
        }

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        var created = await GetPostWithIncludes(post.Id);
        return ApiResponse<PostResponse>.Ok(MapToResponse(created!, authorId));
    }

    public async Task<ApiResponse<PostResponse>> GetPostAsync(Guid postId, Guid requesterId)
    {
        var post = await GetPostWithIncludes(postId);
        if (post == null) return ApiResponse<PostResponse>.Fail("Post not found.");

        var result = MapToResponse(post, requesterId);

        // Enrich with the author's (and any shared post author's) current data so
        // a post opened from a notification looks identical to the feed.
        var enriched = await EnrichAuthorsAsync(new List<PostResponse> { result },
            new List<Post> { post }, requesterId);
        result = enriched[0];

        return ApiResponse<PostResponse>.Ok(result);
    }

    public async Task<ApiResponse<PostResponse>> UpdatePostAsync(
        Guid postId, Guid requesterId, string content, IFormFile? mediaFile)
    {
        var post = await GetPostWithIncludes(postId);
        if (post == null) return ApiResponse<PostResponse>.Fail("Post not found.");
        if (post.AuthorId != requesterId) return ApiResponse<PostResponse>.Fail("Not authorized.");

        post.Content = content;
        post.UpdatedAt = DateTime.UtcNow;

        if (mediaFile != null)
        {
            foreach (var m in post.Media)
                _mediaStorage.DeleteMedia(m.File);

            _db.PostMedia.RemoveRange(post.Media);

            var (filePath, mediaType, thumbnail) =
                await _mediaStorage.SaveMediaAsync(mediaFile, post.Username);

            post.Media = new List<PostMedia>
            {
                new PostMedia
                {
                    File = filePath,
                    MediaType = mediaType,
                    Thumbnail = thumbnail,
                    PostId = post.Id
                }
            };
        }

        await _db.SaveChangesAsync();
        return ApiResponse<PostResponse>.Ok(MapToResponse(post, requesterId));
    }

    public async Task<ApiResponse<bool>> DeletePostAsync(Guid postId, Guid requesterId)
    {
        var post = await _db.Posts
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null) return ApiResponse<bool>.Fail("Post not found.");
        if (post.AuthorId != requesterId) return ApiResponse<bool>.Fail("Not authorized.");

        foreach (var m in post.Media)
            _mediaStorage.DeleteMedia(m.File);

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<int>> RecordViewAsync(Guid postId)
    {
        var post = await _db.Posts.FindAsync(postId);
        if (post == null) return ApiResponse<int>.Fail("Post not found.");
        post.ViewsCount++;
        await _db.SaveChangesAsync();
        return ApiResponse<int>.Ok(post.ViewsCount);
    }

    public async Task<ApiResponse<FeedResponse>> GetReelFeedAsync(Guid userId, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;

        var query = _db.Posts
            .Where(p => p.IsReel)
            .Include(p => p.Media)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync();
        var reels = await query.Skip(skip).Take(pageSize).ToListAsync();

        return ApiResponse<FeedResponse>.Ok(
            new FeedResponse(reels.Select(r => MapToResponse(r, userId)).ToList(), total, null, null));
    }

    public async Task<ApiResponse<PostResponse>> CreateReelAsync(
        Guid authorId, string username, string avatar,
        string caption, IFormFile videoFile)
    {
        var (filePath, mediaType, thumbnail) =
            await _mediaStorage.SaveMediaAsync(videoFile, username);

        var reel = new Post
        {
            AuthorId = authorId,
            Username = username,
            Avatar = avatar,
            Content = caption,
            Type = "reel",
            IsReel = true
        };

        reel.Media.Add(new PostMedia
        {
            File = filePath,
            MediaType = "video",
            Thumbnail = thumbnail,
            PostId = reel.Id
        });

        _db.Posts.Add(reel);
        await _db.SaveChangesAsync();

        return ApiResponse<PostResponse>.Ok(MapToResponse(reel, authorId));
    }

    public async Task<ApiResponse<FeedResponse>> GetUserPostsAsync(
        Guid authorId, Guid requesterId, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;

        var posts = await _db.Posts
            .Where(p => p.AuthorId == authorId && !p.IsReel)
            .Include(p => p.Media)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .Include(p => p.SharedPost).ThenInclude(sp => sp != null ? sp.Media : null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(pageSize)
            .ToListAsync();

        var total = await _db.Posts.CountAsync(p => p.AuthorId == authorId && !p.IsReel);

        var results = posts.Select(p => MapToResponse(p, requesterId)).ToList();

        // Enrich the author's own posts (and any shared post) with current data.
        results = await EnrichAuthorsAsync(results, posts, requesterId);

        return ApiResponse<FeedResponse>.Ok(new FeedResponse(results, total, null, null));
    }

    // Posts that reposted a given post (the repost list).
    public async Task<ApiResponse<FeedResponse>> GetRepostsAsync(
        Guid postId, Guid requesterId, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;

        var query = _db.Posts.Where(p => p.SharedPostId == postId);
        var total = await query.CountAsync();

        var posts = await query
            .Include(p => p.Media)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .Include(p => p.SharedPost).ThenInclude(sp => sp != null ? sp.Media : null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip).Take(pageSize)
            .ToListAsync();

        var results = posts.Select(p => MapToResponse(p, requesterId)).ToList();

        results = await EnrichAuthorsAsync(results, posts, requesterId);

        return ApiResponse<FeedResponse>.Ok(new FeedResponse(results, total, null, null));
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetCategoriesAsync()
    {
        var cats = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return ApiResponse<List<CategoryDto>>.Ok(
            cats.Select(c => new CategoryDto(c.Id.ToString(), c.Name, c.Description)).ToList());
    }

    private async Task<Post?> GetPostWithIncludes(Guid postId) =>
        await _db.Posts
            .Include(p => p.Media)
            .Include(p => p.Categories).ThenInclude(pc => pc.Category)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .Include(p => p.SharedPost).ThenInclude(sp => sp != null ? sp.Media : null)
            .FirstOrDefaultAsync(p => p.Id == postId);

    // Enriches both the post authors and any shared/original post authors with
    // their current avatar, occupation and follow state from the profile service.
    private async Task<List<PostResponse>> EnrichAuthorsAsync(
        List<PostResponse> results, List<Post> posts, Guid requesterId)
    {
        var authorIds = posts.Select(p => p.AuthorId)
            .Concat(posts.Where(p => p.SharedPost != null)
                         .Select(p => p.SharedPost!.AuthorId))
            .Distinct();

        var authors = await _avatarResolver.ResolveAuthorsAsync(authorIds, requesterId);
        if (authors.Count == 0) return results;

        return results.Select(r =>
        {
            var updated = r;

            if (authors.TryGetValue(r.UserId, out var info))
            {
                updated = updated with
                {
                    Avatar = string.IsNullOrEmpty(info.Avatar) ? updated.Avatar : info.Avatar,
                    Occupation = info.Occupation,
                    IsFollowed = info.IsFollowed,
                };
            }

            if (updated.SharedPostDetails is { } shared &&
                authors.TryGetValue(shared.UserId, out var sharedInfo) &&
                !string.IsNullOrEmpty(sharedInfo.Avatar))
            {
                updated = updated with
                {
                    SharedPostDetails = shared with { Avatar = sharedInfo.Avatar }
                };
            }

            return updated;
        }).ToList();
    }

    private PostResponse MapToResponse(Post post, Guid requesterId)
    {
        var userReaction = post.Reactions
            .FirstOrDefault(r => r.AuthorId == requesterId)?.Type;

        SharedPostDetailsDto? sharedDetails = null;
        if (post.SharedPost != null)
        {
            sharedDetails = new SharedPostDetailsDto(
                post.SharedPost.Id.ToString(),
                post.SharedPost.AuthorId.ToString(),
                post.SharedPost.Username,
                post.SharedPost.Username,
                string.IsNullOrEmpty(post.SharedPost.Avatar)
                    ? null
                    : _mediaStorage.ResolvePublicUrl(post.SharedPost.Avatar),
                post.SharedPost.Content,
                post.SharedPost.CreatedAt,
                post.SharedPost.Media.Select(m => new PostMediaDto(
                    m.Id.ToString(),
                    _mediaStorage.ResolvePublicUrl(m.File),
                    m.MediaType,
                    m.Thumbnail != null ? _mediaStorage.ResolvePublicUrl(m.Thumbnail) : null
                )).ToList()
            );
        }

        return new PostResponse(
            Id: post.Id.ToString(),
            UserId: post.AuthorId.ToString(),
            Username: post.Username,
            Avatar: _mediaStorage.ResolvePublicUrl(post.Avatar),
            Occupation: null,
            Content: post.Content,
            Type: post.Type,
            IsReel: post.IsReel,
            Media: post.Media.Select(m => new PostMediaDto(
                m.Id.ToString(),
                _mediaStorage.ResolvePublicUrl(m.File),
                m.MediaType,
                m.Thumbnail != null ? _mediaStorage.ResolvePublicUrl(m.Thumbnail) : null
            )).ToList(),
            CategoriesDetail: post.Categories.Select(pc => new CategoryDto(
                pc.Category.Id.ToString(),
                pc.Category.Name,
                pc.Category.Description
            )).ToList(),
            ReactionsCount: post.Reactions.Count,
            CommentsCount: post.Comments.Count(c => c.ParentId == null),
            ShareCount: post.Reposts.Count,
            ViewsCount: post.ViewsCount,
            CurrentUserReaction: userReaction,
            IsFollowed: false,
            SharedPost: post.SharedPostId?.ToString(),
            SharedPostDetails: sharedDetails,
            CreatedAt: post.CreatedAt
        );
    }
}
