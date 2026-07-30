using System.Text;
using System.Text.Json;

namespace FeedService.Services;

public interface IProfileAvatarResolver
{
    /// <summary>
    /// Returns auth_user_id -> current avatar URL for the given authors by asking
    /// the profile service. Never throws; returns an empty map on any failure.
    /// </summary>
    Task<Dictionary<string, string?>> ResolveAsync(IEnumerable<Guid> authorIds);
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
}
