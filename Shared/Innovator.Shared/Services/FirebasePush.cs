using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovator.Shared.Services;

public interface IFirebasePushSender
{
    Task<IReadOnlyList<string>> SendToTokensAsync(
        IEnumerable<string> tokens,
        string title,
        string body,
        IDictionary<string, string>? data = null);
}

public sealed class FirebasePushSender : IFirebasePushSender
{
    private readonly FirebaseApp _app;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(FirebaseApp app, ILogger<FirebasePushSender> logger)
    {
        _app = app;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> SendToTokensAsync(
        IEnumerable<string> tokens, string title, string body, IDictionary<string, string>? data = null)
    {
        var list = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();

        if (list.Count == 0)
            return Array.Empty<string>();

        var messaging = FirebaseMessaging.GetMessaging(_app);
        var invalid = new List<string>();

        foreach (var batch in Chunk(list, 500))
        {
            var message = new MulticastMessage
            {
                Tokens = batch,
                Notification = new Notification { Title = title, Body = body },
                Data = data is null ? null : new Dictionary<string, string>(data)
            };

            try
            {
                var response = await messaging.SendEachForMulticastAsync(message);
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var r = response.Responses[i];
                    if (r.IsSuccess) continue;

                    var code = r.Exception?.MessagingErrorCode;
                    if (code == MessagingErrorCode.Unregistered || code == MessagingErrorCode.InvalidArgument)
                        invalid.Add(batch[i]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FCM multicast send failed for {Count} tokens", batch.Count);
            }
        }

        return invalid;
    }

    private static IEnumerable<List<string>> Chunk(List<string> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}

public sealed class NoopPushSender : IFirebasePushSender
{
    private readonly ILogger<NoopPushSender> _logger;
    public NoopPushSender(ILogger<NoopPushSender> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> SendToTokensAsync(
        IEnumerable<string> tokens, string title, string body, IDictionary<string, string>? data = null)
    {
        _logger.LogInformation("Firebase not configured; skipping push \"{Title}\".", title);
        return Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());
    }
}

public static class FirebasePushServiceCollectionExtensions
{
    public static IServiceCollection AddFirebasePush(this IServiceCollection services, IConfiguration config)
    {
        var credentialsPath = config["Firebase:CredentialsPath"];

        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            // FirebaseApp is a process-wide singleton; create it once.
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialsPath)
            });

            services.AddSingleton(app);
            services.AddSingleton<IFirebasePushSender, FirebasePushSender>();
            Console.WriteLine($"[Firebase] Push enabled using credentials at {credentialsPath}.");
        }
        else
        {
            // Make a misconfiguration obvious rather than silently dropping pushes.
            if (!string.IsNullOrWhiteSpace(credentialsPath))
                Console.WriteLine(
                    $"[Firebase] Firebase:CredentialsPath set but file not found at " +
                    $"'{credentialsPath}'; push disabled (using no-op sender).");
            else
                Console.WriteLine(
                    "[Firebase] No Firebase:CredentialsPath configured; push disabled (using no-op sender).");

            services.AddSingleton<IFirebasePushSender, NoopPushSender>();
        }

        return services;
    }
}
