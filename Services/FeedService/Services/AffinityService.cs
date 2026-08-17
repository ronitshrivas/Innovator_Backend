using FeedService.Data;
using FeedService.Entities;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

/// Recomputes precomputed affinity tables from recent engagement. Run nightly.
/// A full recompute is simple and correct at current scale; switch to
/// incremental only if the nightly run becomes slow.
public interface IAffinityService
{
    Task RecomputeAllAsync(CancellationToken ct = default);

    /// <summary>viewer→author scores for the given authors (precomputed).</summary>
    Task<Dictionary<string, double>> GetAuthorAffinityAsync(Guid userId, IEnumerable<Guid> authorIds);

    /// <summary>viewer's top category ids (precomputed, falls back to empty).</summary>
    Task<HashSet<string>> GetTopCategoriesAsync(Guid userId);
}

public class AffinityService : IAffinityService
{
    private readonly FeedDbContext _db;

    // Only consider engagement from the recent past.
    private static readonly int LookbackDays = 90;

    public AffinityService(FeedDbContext db) => _db = db;

    public async Task RecomputeAllAsync(CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-LookbackDays);

        await RecomputeUserUserAsync(since, ct);
        await RecomputeUserCategoryAsync(since, ct);
    }

    // viewer→author: how much a viewer engages with a specific author, from the
    // viewer's reactions and comments on that author's posts.
    private async Task RecomputeUserUserAsync(DateTime since, CancellationToken ct)
    {
        // reactions: (reactor, post author) with weight 1
        var reactionPairs = await _db.Reactions
            .Where(r => r.CreatedAt > since)
            .Join(_db.Posts, r => r.PostId, p => p.Id, (r, p) => new { r.AuthorId, Target = p.AuthorId })
            .Where(x => x.AuthorId != x.Target)
            .GroupBy(x => new { x.AuthorId, x.Target })
            .Select(g => new { g.Key.AuthorId, g.Key.Target, Count = g.Count() })
            .ToListAsync(ct);

        // comments weigh a bit more than reactions
        var commentPairs = await _db.Comments
            .Where(c => c.CreatedAt > since && c.PostId != null)
            .Join(_db.Posts, c => c.PostId!.Value, p => p.Id, (c, p) => new { c.AuthorId, Target = p.AuthorId })
            .Where(x => x.AuthorId != x.Target)
            .GroupBy(x => new { x.AuthorId, x.Target })
            .Select(g => new { g.Key.AuthorId, g.Key.Target, Count = g.Count() })
            .ToListAsync(ct);

        var scores = new Dictionary<(Guid, Guid), double>();
        foreach (var r in reactionPairs)
            scores[(r.AuthorId, r.Target)] = scores.GetValueOrDefault((r.AuthorId, r.Target)) + r.Count * 1.0;
        foreach (var c in commentPairs)
            scores[(c.AuthorId, c.Target)] = scores.GetValueOrDefault((c.AuthorId, c.Target)) + c.Count * 2.0;

        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"UserUserAffinities\";", ct);

        var rows = scores.Select(kv => new UserUserAffinity
        {
            UserId = kv.Key.Item1,
            TargetUserId = kv.Key.Item2,
            Score = kv.Value
        });
        _db.UserUserAffinities.AddRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    // viewer→category: from categories on posts the viewer reacted to/commented on.
    private async Task RecomputeUserCategoryAsync(DateTime since, CancellationToken ct)
    {
        var reactionCats = await _db.Reactions
            .Where(r => r.CreatedAt > since)
            .Join(_db.PostCategories, r => r.PostId, pc => pc.PostId,
                  (r, pc) => new { r.AuthorId, pc.CategoryId })
            .GroupBy(x => new { x.AuthorId, x.CategoryId })
            .Select(g => new { g.Key.AuthorId, g.Key.CategoryId, Count = g.Count() })
            .ToListAsync(ct);

        var commentCats = await _db.Comments
            .Where(c => c.CreatedAt > since && c.PostId != null)
            .Join(_db.PostCategories, c => c.PostId!.Value, pc => pc.PostId,
                  (c, pc) => new { c.AuthorId, pc.CategoryId })
            .GroupBy(x => new { x.AuthorId, x.CategoryId })
            .Select(g => new { g.Key.AuthorId, g.Key.CategoryId, Count = g.Count() })
            .ToListAsync(ct);

        var scores = new Dictionary<(Guid, Guid), double>();
        foreach (var r in reactionCats)
            scores[(r.AuthorId, r.CategoryId)] = scores.GetValueOrDefault((r.AuthorId, r.CategoryId)) + r.Count;
        foreach (var c in commentCats)
            scores[(c.AuthorId, c.CategoryId)] = scores.GetValueOrDefault((c.AuthorId, c.CategoryId)) + c.Count * 2.0;

        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"UserCategoryAffinities\";", ct);

        var rows = scores.Select(kv => new UserCategoryAffinity
        {
            UserId = kv.Key.Item1,
            CategoryId = kv.Key.Item2,
            Score = kv.Value
        });
        _db.UserCategoryAffinities.AddRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, double>> GetAuthorAffinityAsync(
        Guid userId, IEnumerable<Guid> authorIds)
    {
        var ids = authorIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _db.UserUserAffinities
            .Where(a => a.UserId == userId && ids.Contains(a.TargetUserId))
            .Select(a => new { a.TargetUserId, a.Score })
            .ToListAsync();

        return rows.ToDictionary(r => r.TargetUserId.ToString(), r => r.Score);
    }

    public async Task<HashSet<string>> GetTopCategoriesAsync(Guid userId)
    {
        var rows = await _db.UserCategoryAffinities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Score)
            .Take(5)
            .Select(a => a.CategoryId)
            .ToListAsync();

        return rows.Select(id => id.ToString()).ToHashSet();
    }
}
