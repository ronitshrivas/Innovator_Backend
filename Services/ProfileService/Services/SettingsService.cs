using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using ProfileService.Data;
using ProfileService.DTOs;
using ProfileService.Entities;

namespace ProfileService.Services;

public interface ISettingsService
{
    Task<ApiResponse<SettingsResponse>> GetAsync(Guid userId);
    Task<ApiResponse<SettingsResponse>> UpdateAsync(Guid userId, UpdateSettingsRequest request);
    Task<ApiResponse<SettingsResponse>> ResetAsync(Guid userId);
    Task<List<SettingsFlags>> GetFlagsAsync(IEnumerable<Guid> userIds);
}

public class SettingsService : ISettingsService
{
    private static readonly HashSet<string> AudienceValues =
        new(StringComparer.OrdinalIgnoreCase) { "everyone", "followers", "none" };

    private static readonly HashSet<string> ThemeValues =
        new(StringComparer.OrdinalIgnoreCase) { "system", "light", "dark" };

    private readonly ProfileDbContext _db;

    public SettingsService(ProfileDbContext db) => _db = db;

    public async Task<ApiResponse<SettingsResponse>> GetAsync(Guid userId)
    {
        var settings = await GetOrCreateAsync(userId);
        return ApiResponse<SettingsResponse>.Ok(MapToResponse(settings));
    }

    public async Task<ApiResponse<SettingsResponse>> UpdateAsync(
        Guid userId, UpdateSettingsRequest request)
    {
        if (request.WhoCanMessage is not null && !AudienceValues.Contains(request.WhoCanMessage))
            return ApiResponse<SettingsResponse>.Fail("who_can_message must be everyone, followers or none.");
        if (request.WhoCanComment is not null && !AudienceValues.Contains(request.WhoCanComment))
            return ApiResponse<SettingsResponse>.Fail("who_can_comment must be everyone, followers or none.");
        if (request.Theme is not null && !ThemeValues.Contains(request.Theme))
            return ApiResponse<SettingsResponse>.Fail("theme must be system, light or dark.");

        var s = await GetOrCreateAsync(userId);

        if (request.PushEnabled is not null) s.PushEnabled = request.PushEnabled.Value;
        if (request.NotifyLikes is not null) s.NotifyLikes = request.NotifyLikes.Value;
        if (request.NotifyComments is not null) s.NotifyComments = request.NotifyComments.Value;
        if (request.NotifyFollows is not null) s.NotifyFollows = request.NotifyFollows.Value;
        if (request.NotifyMentions is not null) s.NotifyMentions = request.NotifyMentions.Value;
        if (request.NotifyMessages is not null) s.NotifyMessages = request.NotifyMessages.Value;
        if (request.NotifyReposts is not null) s.NotifyReposts = request.NotifyReposts.Value;
        if (request.EmailDigest is not null) s.EmailDigest = request.EmailDigest.Value;

        if (request.PrivateAccount is not null) s.PrivateAccount = request.PrivateAccount.Value;
        if (request.WhoCanMessage is not null) s.WhoCanMessage = request.WhoCanMessage.ToLower();
        if (request.WhoCanComment is not null) s.WhoCanComment = request.WhoCanComment.ToLower();
        if (request.ShowActivityStatus is not null) s.ShowActivityStatus = request.ShowActivityStatus.Value;
        if (request.ShowInSearch is not null) s.ShowInSearch = request.ShowInSearch.Value;

        if (request.Language is not null) s.Language = request.Language;
        if (request.Theme is not null) s.Theme = request.Theme.ToLower();
        if (request.Timezone is not null) s.Timezone = request.Timezone;

        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<SettingsResponse>.Ok(MapToResponse(s), "Settings updated.");
    }

    public async Task<ApiResponse<SettingsResponse>> ResetAsync(Guid userId)
    {
        var s = await GetOrCreateAsync(userId);
        var defaults = new UserSettings { UserId = userId, Id = s.Id, CreatedAt = s.CreatedAt };

        s.PushEnabled = defaults.PushEnabled;
        s.NotifyLikes = defaults.NotifyLikes;
        s.NotifyComments = defaults.NotifyComments;
        s.NotifyFollows = defaults.NotifyFollows;
        s.NotifyMentions = defaults.NotifyMentions;
        s.NotifyMessages = defaults.NotifyMessages;
        s.NotifyReposts = defaults.NotifyReposts;
        s.EmailDigest = defaults.EmailDigest;
        s.PrivateAccount = defaults.PrivateAccount;
        s.WhoCanMessage = defaults.WhoCanMessage;
        s.WhoCanComment = defaults.WhoCanComment;
        s.ShowActivityStatus = defaults.ShowActivityStatus;
        s.ShowInSearch = defaults.ShowInSearch;
        s.Language = defaults.Language;
        s.Theme = defaults.Theme;
        s.Timezone = defaults.Timezone;
        s.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ApiResponse<SettingsResponse>.Ok(MapToResponse(s), "Settings reset to defaults.");
    }

    public async Task<List<SettingsFlags>> GetFlagsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _db.UserSettings
            .Where(s => ids.Contains(s.UserId))
            .ToListAsync();

        // Users with no row yet get defaults (a fresh UserSettings instance).
        var present = rows.Select(r => r.UserId).ToHashSet();
        var result = rows.Select(ToFlags).ToList();
        foreach (var missing in ids.Where(id => !present.Contains(id)))
            result.Add(ToFlags(new UserSettings { UserId = missing }));

        return result;
    }

    private async Task<UserSettings> GetOrCreateAsync(Guid userId)
    {
        var existing = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (existing is not null) return existing;

        var created = new UserSettings { UserId = userId };
        _db.UserSettings.Add(created);
        await _db.SaveChangesAsync();
        return created;
    }

    private static SettingsResponse MapToResponse(UserSettings s) => new(
        s.PushEnabled, s.NotifyLikes, s.NotifyComments, s.NotifyFollows,
        s.NotifyMentions, s.NotifyMessages, s.NotifyReposts, s.EmailDigest,
        s.PrivateAccount, s.WhoCanMessage, s.WhoCanComment,
        s.ShowActivityStatus, s.ShowInSearch,
        s.Language, s.Theme, s.Timezone);

    private static SettingsFlags ToFlags(UserSettings s) => new(
        s.UserId.ToString(),
        s.PushEnabled, s.NotifyLikes, s.NotifyComments, s.NotifyFollows,
        s.NotifyMentions, s.NotifyMessages, s.NotifyReposts,
        s.PrivateAccount, s.WhoCanMessage, s.WhoCanComment, s.ShowInSearch);
}
