using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeedService.Services;

/// The viewer's follow graph fetched from the profile service, used to widen
/// feed candidate generation to followed + 2nd-degree authors.
public record FollowGraphResult(
    [property: JsonPropertyName("following")] List<string> Following,
    [property: JsonPropertyName("second_degree")] List<string> SecondDegree);

public interface IFollowGraphClient
{
    /// <summary>Never throws; returns empty graph on failure.</summary>
    Task<FollowGraphResult> GetAsync(Guid userId);
}

public class FollowGraphClient : IFollowGraphClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<FollowGraphClient> _logger;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public FollowGraphClient(IHttpClientFactory factory, ILogger<FollowGraphClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<FollowGraphResult> GetAsync(Guid userId)
    {
        try
        {
            var client = _factory.CreateClient("profile");
            var response = await client.GetAsync($"/api/internal/profiles/{userId}/follow-graph");
            if (!response.IsSuccessStatusCode)
                return new FollowGraphResult(new(), new());

            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FollowGraphResult>(body, Json)
                   ?? new FollowGraphResult(new(), new());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "follow-graph lookup failed; using empty graph.");
            return new FollowGraphResult(new(), new());
        }
    }
}
