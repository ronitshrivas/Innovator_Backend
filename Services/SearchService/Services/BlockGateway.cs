using System.Text.Json;

namespace SearchService.Services;

/// Fetches the set of users blocked in either direction so search can hide them.
public interface IBlockGateway
{
    Task<HashSet<Guid>> GetBlockedIdsAsync(Guid requesterId);
}

public class BlockGateway : IBlockGateway
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BlockGateway> _logger;

    public BlockGateway(IHttpClientFactory factory, ILogger<BlockGateway> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<HashSet<Guid>> GetBlockedIdsAsync(Guid requesterId)
    {
        try
        {
            var client = _factory.CreateClient("profile");
            var response = await client.GetAsync(
                $"/api/internal/profiles/{requesterId}/block-pairs");
            if (!response.IsSuccessStatusCode) return new();

            var body = await response.Content.ReadAsStringAsync();
            var ids = JsonSerializer.Deserialize<List<string>>(body) ?? new();
            return ids.Where(s => Guid.TryParse(s, out _))
                      .Select(Guid.Parse)
                      .ToHashSet();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "block-pairs lookup failed; not filtering blocks.");
            return new();
        }
    }
}
