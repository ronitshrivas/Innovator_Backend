using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeedService.Services;

/// Per-user preference flags fetched from the profile service for enforcement.
public record UserFlags(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("push_enabled")] bool PushEnabled,
    [property: JsonPropertyName("notify_likes")] bool NotifyLikes,
    [property: JsonPropertyName("notify_comments")] bool NotifyComments,
    [property: JsonPropertyName("notify_follows")] bool NotifyFollows,
    [property: JsonPropertyName("notify_mentions")] bool NotifyMentions,
    [property: JsonPropertyName("notify_messages")] bool NotifyMessages,
    [property: JsonPropertyName("notify_reposts")] bool NotifyReposts,
    [property: JsonPropertyName("private_account")] bool PrivateAccount,
    [property: JsonPropertyName("who_can_message")] string WhoCanMessage,
    [property: JsonPropertyName("who_can_comment")] string WhoCanComment,
    [property: JsonPropertyName("show_in_search")] bool ShowInSearch);

public interface ISettingsClient
{
    /// <summary>user_id -> flags. Never throws; missing users simply absent.</summary>
    Task<Dictionary<string, UserFlags>> GetFlagsAsync(IEnumerable<Guid> userIds);
}

public class SettingsClient : ISettingsClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<SettingsClient> _logger;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SettingsClient(IHttpClientFactory factory, ILogger<SettingsClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<Dictionary<string, UserFlags>> GetFlagsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return new();

        try
        {
            var client = _factory.CreateClient("profile");
            var payload = JsonSerializer.Serialize(new { user_ids = ids });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/settings/batch", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("settings batch failed: {Status} {Body}",
                    (int)response.StatusCode, body);
                return new();
            }

            var list = JsonSerializer.Deserialize<List<UserFlags>>(body, Json) ?? new();
            return list.ToDictionary(f => f.UserId, f => f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "settings batch threw; defaulting to empty flags.");
            return new();
        }
    }
}
