namespace Innovator.Shared.Helpers;

/// <summary>
/// Runs a database initialisation action (migrate / ensure-created / seed) with
/// retries, so a service started before Postgres is reachable waits instead of
/// crashing on the first connection or DNS hiccup.
/// </summary>
public static class StartupDb
{
    public static async Task InitializeAsync(Func<Task> initialize, int maxAttempts = 15, int delaySeconds = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await initialize();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Console.WriteLine($"[startup] database not ready (attempt {attempt}/{maxAttempts}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}
