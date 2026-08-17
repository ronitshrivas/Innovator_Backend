using FeedService.Data;
using FeedService.DTOs;
using FeedService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

public interface IFeedService
{
    Task<ApiResponse<FeedResponse>> GetFeedAsync(Guid userId, int page, int pageSize, bool ranked = true);
    Task<ApiResponse<bool>> RecordViewsAsync(Guid userId, IEnumerable<string> postIds);
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

    private readonly IFeedRanker _ranker;
    private readonly IFollowGraphClient _followGraph;
    private readonly IAffinityService _affinity;

    public FeedBusinessService(
        FeedDbContext db,
        IMediaStorageService mediaStorage,
        IProfileAvatarResolver avatarResolver,
        IFeedRanker ranker,
        IFollowGraphClient followGraph,
        IAffinityService affinity)
    {
        _db = db;
        _mediaStorage = mediaStorage;
        _avatarResolver = avatarResolver;
        _ranker = ranker;
        _followGraph = followGraph;
        _affinity = affinity;
    }

    public async Task<ApiResponse<FeedResponse>> GetFeedAsync(
        Guid userId, int page, int pageSize, bool ranked = true)
    {
        var skip = (page - 1) * pageSize;

        var baseQuery = _db.Posts
            .Where(p => !p.IsReel)
            .Include(p => p.Media)
            .Include(p => p.Categories).ThenInclude(pc => pc.Category)
            .Include(p => p.Reactions)
            .Include(p => p.Comments)
            .Include(p => p.SharedPost).ThenInclude(sp => sp != null ? sp.Media : null);

        var total = await _db.Posts.CountAsync(p => !p.IsReel);

        // ---- Chronological fallback (?ranked=false), unchanged behaviour. ----
        if (!ranked)
        {
            var chrono = await baseQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip).Take(pageSize)
                .ToListAsync();

            var chronoResults = chrono.Select(p => MapToResponse(p, userId)).ToList();
            chronoResults = await EnrichAuthorsAsync(chronoResults, chrono, userId);

            var chronoHasNext = skip + chrono.Count < total;
            return ApiResponse<FeedResponse>.Ok(new FeedResponse(
                chronoResults, total,
                chronoHasNext ? $"/api/feed?page={page + 1}&pageSize={pageSize}" : null,
                page > 1 ? $"/api/feed?page={page - 1}&pageSize={pageSize}" : null));
        }

        // ---- Ranked feed ----
        // Stage 1: candidate generation — a deduped union of:
        //   (a) a recent global window (exploration + fresh content),
        //   (b) recent posts from people the viewer follows,
        //   (c) recent posts from 2nd-degree connections.
        const int candidateWindow = 300;
        var graph = await _followGraph.GetAsync(userId);
        var followingIds = graph.Following
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();
        var secondDegreeIds = graph.SecondDegree
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();

        var candidates = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(candidateWindow)
            .ToListAsync();

        var haveIds = candidates.Select(p => p.Id).ToHashSet();

        var networkIds = followingIds.Concat(secondDegreeIds).Distinct().ToList();
        if (networkIds.Count > 0)
        {
            var networkPosts = await baseQuery
                .Where(p => networkIds.Contains(p.AuthorId))
                .OrderByDescending(p => p.CreatedAt)
                .Take(candidateWindow)
                .ToListAsync();

            foreach (var p in networkPosts)
                if (haveIds.Add(p.Id))
                    candidates.Add(p);
        }

        var secondDegreeSet = secondDegreeIds.Select(g => g.ToString()).ToHashSet();

        // View-dedup: drop posts this viewer has already seen (last 7 days).
        // If filtering leaves too few, keep the seen ones (ranked lower later)
        // so new / low-activity users still get a full feed.
        var candidateIds = candidates.Select(p => p.Id).ToList();
        var seenIds = await _db.PostViews
            .Where(v => v.UserId == userId &&
                        v.ViewedAt > DateTime.UtcNow.AddDays(-7) &&
                        candidateIds.Contains(v.PostId))
            .Select(v => v.PostId)
            .ToListAsync();

        if (seenIds.Count > 0)
        {
            var seen = seenIds.ToHashSet();
            var unseen = candidates.Where(p => !seen.Contains(p.Id)).ToList();
            // Only apply the filter if enough remain to fill a few pages.
            if (unseen.Count >= pageSize * 2)
                candidates = unseen;
        }

        var mapped = candidates.Select(p => MapToResponse(p, userId)).ToList();
        mapped = await EnrichAuthorsAsync(mapped, candidates, userId);

        // Viewer's top categories — prefer the nightly precomputed table; fall
        // back to a live query if the job hasn't populated it yet.
        var topCategories = await _affinity.GetTopCategoriesAsync(userId);
        if (topCategories.Count == 0)
            topCategories = await GetViewerTopCategoriesAsync(userId);

        // Precomputed viewer→author affinity for the candidate authors.
        var authorIds = candidates.Select(p => p.AuthorId).Distinct().ToList();
        var authorAffinity = await _affinity.GetAuthorAffinityAsync(userId, authorIds);

        // Stable per-session seed so page 2 continues page 1 rather than
        // re-shuffling. Ties the ordering to the user (swap for a session id
        // if you want a fresh order per app-open).
        var seed = userId.GetHashCode();

        var rankedList = _ranker.Rank(mapped, topCategories, secondDegreeSet, authorAffinity, seed);

        var pageItems = rankedList.Skip(skip).Take(pageSize).ToList();
        var hasNext = skip + pageItems.Count < rankedList.Count;

        return ApiResponse<FeedResponse>.Ok(new FeedResponse(
            pageItems, total,
            hasNext ? $"/api/feed?page={page + 1}&pageSize={pageSize}" : null,
            page > 1 ? $"/api/feed?page={page - 1}&pageSize={pageSize}" : null));
    }

    // The categories the viewer engages with most, derived from the posts they
    // have reacted to or commented on. Used as a ranking signal.
    private async Task<HashSet<string>> GetViewerTopCategoriesAsync(Guid userId)
    {
        var reactedPostIds = await _db.Reactions
            .Where(r => r.AuthorId == userId)
            .Select(r => r.PostId)
            .ToListAsync();

        var commentedPostIds = await _db.Comments
            .Where(c => c.AuthorId == userId && c.PostId != null)
            .Select(c => c.PostId!.Value)
            .ToListAsync();

        var engagedPostIds = reactedPostIds.Concat(commentedPostIds).Distinct().ToList();
        if (engagedPostIds.Count == 0) return new();

        var categoryCounts = await _db.PostCategories
            .Where(pc => engagedPostIds.Contains(pc.PostId))
            .GroupBy(pc => pc.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        return categoryCounts.Select(x => x.CategoryId.ToString()).ToHashSet();
    }

    // Records which posts a viewer has seen (batch, idempotent). Silently skips
    // posts already recorded so the app can report freely as the user scrolls.
    public async Task<ApiResponse<bool>> RecordViewsAsync(Guid userId, IEnumerable<string> postIds)
    {
        var ids = postIds
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return ApiResponse<bool>.Ok(true);

        var already = await _db.PostViews
            .Where(v => v.UserId == userId && ids.Contains(v.PostId))
            .Select(v => v.PostId)
            .ToListAsync();

        var existing = already.ToHashSet();
        var now = DateTime.UtcNow;
        var fresh = ids.Where(id => !existing.Contains(id))
            .Select(id => new PostView { UserId = userId, PostId = id, ViewedAt = now })
            .ToList();

        if (fresh.Count > 0)
        {
            _db.PostViews.AddRange(fresh);
            await _db.SaveChangesAsync();
        }

        return ApiResponse<bool>.Ok(true);
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
