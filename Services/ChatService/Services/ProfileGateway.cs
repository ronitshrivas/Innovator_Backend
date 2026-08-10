using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatService.Services;

/// Reads the target's messaging preference and follow relationship from the
/// profile service so the chat service can enforce who_can_message.
public interface IProfileGateway
{
    Task<string> GetWhoCanMessageAsync(Guid targetUserId);
    Task<bool> IsFollowedByAsync(Guid targetUserId, Guid requesterId);
}

public class ProfileGateway : IProfileGateway
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ProfileGateway> _logger;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ProfileGateway(IHttpClientFactory factory, ILogger<ProfileGateway> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    private record Flags(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("who_can_message")] string WhoCanMessage);

    private record AuthorInfoDto(
        [property: JsonPropertyName("is_followed")] bool IsFollowed);

    public async Task<string> GetWhoCanMessageAsync(Guid targetUserId)
    {
        try
        {
            var client = _factory.CreateClient("profile");
            var payload = JsonSerializer.Serialize(new { user_ids = new[] { targetUserId.ToString() } });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/settings/batch", content);
            if (!response.IsSuccessStatusCode) return "everyone";

            var body = await response.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<List<Flags>>(body, Json) ?? new();
            return list.FirstOrDefault()?.WhoCanMessage ?? "everyone";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "who_can_message lookup failed; defaulting to everyone.");
            return "everyone";
        }
    }

    public async Task<bool> IsFollowedByAsync(Guid targetUserId, Guid requesterId)
    {
        try
        {
            var client = _factory.CreateClient("profile");
            var payload = JsonSerializer.Serialize(new
            {
                auth_user_ids = new[] { targetUserId.ToString() },
                requester_id = requesterId.ToString(),
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/api/internal/profiles/author-info", content);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync();
            var map = JsonSerializer.Deserialize<Dictionary<string, AuthorInfoDto>>(body, Json);
            return map != null
                   && map.TryGetValue(targetUserId.ToString(), out var info)
                   && info.IsFollowed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "is-follower lookup failed; defaulting to false.");
            return false;
        }
    }
}
