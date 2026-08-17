namespace FeedService.Services;

/// Nightly background job that refreshes the precomputed affinity tables.
/// Runs an initial pass shortly after startup, then daily.
///
/// Single-instance safe. If FeedService is ever scaled horizontally, gate this
/// behind a DB advisory lock so only one instance recomputes.
public class AffinityJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AffinityJob> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    public AffinityJob(IServiceProvider services, ILogger<AffinityJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(InitialDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var affinity = scope.ServiceProvider.GetRequiredService<IAffinityService>();
            await affinity.RecomputeAllAsync(ct);
            _logger.LogInformation("Affinity tables recomputed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Affinity recompute failed; will retry next cycle.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
