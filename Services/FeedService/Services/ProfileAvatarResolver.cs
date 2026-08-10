using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeedService.Services;

/// Per-author info the feed pulls from the profile service.
public record AuthorInfo(string? Avatar, string? Occupation, bool IsFollowed, string? Username = null);

/// Request body for the profile service's internal author-info lookup.
/// Property names are pinned to snake_case so the wire format never depends
/// on this service's global JSON naming policy.
public record AuthorInfoRequest(
    [property: JsonPropertyName("auth_user_ids")] List<string> AuthUserIds,
    [property: JsonPropertyName("requester_id")] string? RequesterId);

/// Response shape for a single author. Snake_case keys match what the profile
/// service emits (it serializes with a snake_case naming policy).
public record AuthorInfoDto(
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("occupation")] string? Occupation,
    [property: JsonPropertyName("is_followed")] bool IsFollowed,
    [property: JsonPropertyName("username")] string? Username);

public interface IProfileAvatarResolver
{
    /// <summary>author auth_user_id -> current avatar URL. Never throws.</summary>
    Task<Dictionary<string, string?>> ResolveAsync(IEnumerable<Guid> authorIds);

    /// <summary>
    /// author auth_user_id -> {avatar, occupation, is_followed} for the given
    /// requester. Never throws; empty map on failure.
    /// </summary>
    Task<Dictionary<string, AuthorInfo>> ResolveAuthorsAsync(
        IEnumerable<Guid> authorIds, Guid? requesterId);
}

public class ProfileAvatarResolver : IProfileAvatarResolver
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ProfileAvatarResolver> _logger;

    private static readonly JsonSerializerOptions SnakeCaseJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ProfileAvatarResolver(
        IHttpClientFactory factory,
        ILogger<ProfileAvatarResolver> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<Dictionary<string, string?>> ResolveAsync(IEnumerable<Guid> authorIds)
    {
        var ids = authorIds.Distinct().Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return new();

        try
        {
            var client = _factory.CreateClient("profile");
            var payload = JsonSerializer.Serialize(new { auth_user_ids = ids });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/profiles/avatars", content);
            if (!response.IsSuccessStatusCode) return new();

            var body = await response.Content.ReadAsStringAsync();
            var map = JsonSerializer.Deserialize<Dictionary<string, string?>>(body, SnakeCaseJson);
            return map ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Avatar resolve failed; defaulting to empty map.");
            return new();
        }
    }

    public async Task<Dictionary<string, AuthorInfo>> ResolveAuthorsAsync(
        IEnumerable<Guid> authorIds, Guid? requesterId)
    {
        var ids = authorIds.Distinct().Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return new();

        try
        {
            var client = _factory.CreateClient("profile");

            // Explicit DTO guarantees snake_case keys on the wire, regardless of
            // any global JSON naming policy in this service.
            var request = new AuthorInfoRequest(ids, requesterId?.ToString());
            var payload = JsonSerializer.Serialize(request);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/profiles/author-info", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "author-info lookup failed: {Status} body={Body}",
                    (int)response.StatusCode, body);
                return new();
            }

            var raw = JsonSerializer.Deserialize<Dictionary<string, AuthorInfoDto>>(body, SnakeCaseJson);
            if (raw == null || raw.Count == 0)
            {
                _logger.LogWarning(
                    "author-info returned no entries for {Count} ids; body={Body}",
                    ids.Count, body);
                return new();
            }

            return raw.ToDictionary(
                kv => kv.Key,
                kv => new AuthorInfo(
                    kv.Value.Avatar, kv.Value.Occupation, kv.Value.IsFollowed, kv.Value.Username));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "author-info resolve threw; defaulting to empty map.");
            return new();
        }
    }
}
