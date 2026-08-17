using FeedService.DTOs;

namespace FeedService.Services;

/// Tunable ranking weights (bound from appsettings "FeedRanking"), so scoring
/// can be adjusted without a redeploy.
public class FeedRankingOptions
{
    public double Affinity { get; set; } = 3.0;        // viewer follows the author
    public double SecondDegree { get; set; } = 1.5;    // author is a 2nd-degree connection
    public double Engagement { get; set; } = 1.5;      // reactions+comments+reposts / age
    public double Recency { get; set; } = 2.0;         // exponential decay on created_at
    public double CategoryMatch { get; set; } = 1.2;   // post category ∈ viewer's top categories
    public double Media { get; set; } = 0.4;           // has image/video
    public double AuthorQuality { get; set; } = 0.6;   // log-scaled engagement footprint
    public double ColdStart { get; set; } = 0.5;       // brand-new, no engagement yet
    public double Exploration { get; set; } = 0.3;     // random jitter to avoid stagnation
    public double AuthorDiversityPenalty { get; set; } = 1.0; // per repeat from same author

    public double RecencyHalfLifeHours { get; set; } = 24.0;
    public double EngagementHalfLifeHours { get; set; } = 36.0;
}

/// Two-stage ranker: candidates are supplied by the caller (candidate
/// generation), this ranks them. Swap the implementation behind this interface
/// for a learned model later without touching controllers.
public interface IFeedRanker
{
    List<PostResponse> Rank(
        IReadOnlyList<PostResponse> candidates,
        IReadOnlyCollection<string> viewerTopCategoryIds,
        IReadOnlyCollection<string> secondDegreeAuthorIds,
        IReadOnlyDictionary<string, double> authorAffinity,
        int seed);
}

public class HeuristicFeedRanker : IFeedRanker
{
    private readonly FeedRankingOptions _w;

    public HeuristicFeedRanker(FeedRankingOptions weights) => _w = weights;

    public List<PostResponse> Rank(
        IReadOnlyList<PostResponse> candidates,
        IReadOnlyCollection<string> viewerTopCategoryIds,
        IReadOnlyCollection<string> secondDegreeAuthorIds,
        IReadOnlyDictionary<string, double> authorAffinity,
        int seed)
    {
        var now = DateTime.UtcNow;
        var rng = new Random(seed); // deterministic per request/session → stable paging
        var topCats = viewerTopCategoryIds as HashSet<string>
                      ?? new HashSet<string>(viewerTopCategoryIds);
        var secondDegree = secondDegreeAuthorIds as HashSet<string>
                      ?? new HashSet<string>(secondDegreeAuthorIds);

        // Normalise author affinity to 0..1 so its weight stays comparable.
        var maxAff = authorAffinity.Count > 0 ? authorAffinity.Values.Max() : 0.0;

        // Score every candidate.
        var scored = candidates
            .Select(p => new Scored(p, Score(p, now, topCats, secondDegree, authorAffinity, maxAff, rng)))
            .ToList();

        // Order by score, then apply author-diversity: push down consecutive
        // posts from an author already seen higher in the list.
        var ordered = scored.OrderByDescending(s => s.Value).ToList();
        var seenAuthorCount = new Dictionary<string, int>();
        foreach (var s in ordered)
        {
            var count = seenAuthorCount.TryGetValue(s.Post.UserId, out var c) ? c : 0;
            if (count > 0)
                s.Value -= _w.AuthorDiversityPenalty * count;
            seenAuthorCount[s.Post.UserId] = count + 1;
        }

        return ordered
            .OrderByDescending(s => s.Value)
            .Select(s => s.Post)
            .ToList();
    }

    private double Score(
        PostResponse p, DateTime now, HashSet<string> topCats,
        HashSet<string> secondDegree, IReadOnlyDictionary<string, double> authorAffinity,
        double maxAff, Random rng)
    {
        var ageHours = Math.Max(0.0, (now - p.CreatedAt).TotalHours);

        // Recency: exponential decay.
        var recency = Math.Pow(0.5, ageHours / _w.RecencyHalfLifeHours);

        // Engagement velocity: interactions per unit age, time-decayed.
        var interactions = p.ReactionsCount + p.CommentsCount + p.ShareCount;
        var velocity = interactions / (ageHours + 2.0);
        var engagement = velocity * Math.Pow(0.5, ageHours / _w.EngagementHalfLifeHours);

        // Affinity: strongest when the viewer follows the author; otherwise use
        // the precomputed viewer→author engagement affinity (normalised 0..1).
        var graded = 0.0;
        if (maxAff > 0 && authorAffinity.TryGetValue(p.UserId, out var aff))
            graded = aff / maxAff;
        var affinity = p.IsFollowed ? 1.0 : graded;

        // 2nd-degree: author is followed by someone the viewer follows.
        var secondDegreeSignal = (!p.IsFollowed && secondDegree.Contains(p.UserId)) ? 1.0 : 0.0;

        // Category match.
        var categoryMatch = p.CategoriesDetail.Any(c => topCats.Contains(c.Id)) ? 1.0 : 0.0;

        // Content signal: media-bearing posts get a small boost.
        var media = p.Media.Count > 0 ? 1.0 : 0.0;

        // Author quality: log-scaled engagement footprint (dampened).
        var authorQuality = Math.Log10(interactions + 1);

        // Cold-start: brand-new post with no engagement yet gets a small lift.
        var coldStart = (interactions == 0 && ageHours < 6) ? 1.0 : 0.0;

        // Exploration jitter.
        var exploration = rng.NextDouble();

        return _w.Affinity * affinity
             + _w.SecondDegree * secondDegreeSignal
             + _w.Engagement * engagement
             + _w.Recency * recency
             + _w.CategoryMatch * categoryMatch
             + _w.Media * media
             + _w.AuthorQuality * authorQuality
             + _w.ColdStart * coldStart
             + _w.Exploration * exploration;
    }

    private sealed class Scored
    {
        public Scored(PostResponse post, double value)
        {
            Post = post;
            Value = value;
        }

        public PostResponse Post { get; }
        public double Value { get; set; }
    }
}
