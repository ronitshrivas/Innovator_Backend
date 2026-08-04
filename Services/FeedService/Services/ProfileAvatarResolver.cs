using System.Text;
using System.Text.Json;

namespace FeedService.Services;

/// Per-author info the feed pulls from the profile service.
public record AuthorInfo(string? Avatar, string? Occupation, bool IsFollowed);

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

    public ProfileAvatarResolver(IHttpClientFactory factory) => _factory = factory;

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
            var map = JsonSerializer.Deserialize<Dictionary<string, string?>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return map ?? new();
        }
        catch
        {
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
            var payload = JsonSerializer.Serialize(new
            {
                auth_user_ids = ids,
                requester_id = requesterId?.ToString(),
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/profiles/author-info", content);
            if (!response.IsSuccessStatusCode) return new();

            var body = await response.Content.ReadAsStringAsync();
            var map = JsonSerializer.Deserialize<Dictionary<string, AuthorInfo>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return map ?? new();
        }
        catch
        {
            return new();
        }
    }
}
