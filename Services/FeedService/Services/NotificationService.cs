using FeedService.Data;
using FeedService.DTOs;
using FeedService.Entities;
using Innovator.Shared.DTOs;
using Innovator.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

public interface INotificationService
{
    Task CreateAsync(CreateNotificationRequest request);
    Task<List<NotificationDto>> GetForUserAsync(Guid userId);
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, string notificationId);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId);
    Task<FcmTokenResponse> RegisterTokenAsync(Guid userId, FcmTokenRequest request);
    Task<ApiResponse<bool>> DeleteTokenAsync(Guid userId, string tokenId);
}

public class NotificationService : INotificationService
{
    private readonly FeedDbContext _db;
    private readonly IFirebasePushSender _push;

    public NotificationService(FeedDbContext db, IFirebasePushSender push)
    {
        _db = db;
        _push = push;
    }

    public async Task CreateAsync(CreateNotificationRequest r)
    {
        // Never notify yourself.
        if (r.SenderId.HasValue && r.SenderId.Value == r.UserId) return;

        var notification = new Notification
        {
            UserId = r.UserId,
            Title = r.Title,
            Message = r.Message,
            Type = r.Type,
            SenderId = r.SenderId,
            SenderUsername = r.SenderUsername,
            SenderAvatar = r.SenderAvatar,
            RelatedPostId = r.RelatedPostId
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await PushAsync(notification);
    }

    private async Task PushAsync(Notification n)
    {
        var tokens = await _db.FcmTokens
            .Where(t => t.UserId == n.UserId)
            .Select(t => t.Token)
            .ToListAsync();

        if (tokens.Count == 0) return;

        var data = new Dictionary<string, string>
        {
            ["type"] = n.Type,
            ["notification_id"] = n.Id.ToString(),
            ["related_post_id"] = n.RelatedPostId?.ToString() ?? string.Empty
        };

        var invalid = await _push.SendToTokensAsync(tokens, n.Title, n.Message, data);

        if (invalid.Count > 0)
        {
            var stale = await _db.FcmTokens
                .Where(t => t.UserId == n.UserId && invalid.Contains(t.Token))
                .ToListAsync();
            _db.FcmTokens.RemoveRange(stale);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<NotificationDto>> GetForUserAsync(Guid userId)
    {
        var items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .AsNoTracking()
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, string notificationId)
    {
        if (!Guid.TryParse(notificationId, out var id))
            return ApiResponse<bool>.Fail("Invalid notification id.");

        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is null) return ApiResponse<bool>.Fail("Notification not found.");

        n.IsRead = true;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<FcmTokenResponse> RegisterTokenAsync(Guid userId, FcmTokenRequest request)
    {
        var existing = await _db.FcmTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

        if (existing is null)
        {
            existing = new FeedFcmToken
            {
                UserId = userId,
                Token = request.Token,
                DeviceName = request.DeviceName
            };
            _db.FcmTokens.Add(existing);
        }
        else
        {
            existing.DeviceName = request.DeviceName;
        }

        await _db.SaveChangesAsync();
        return new FcmTokenResponse(existing.Id.ToString(), existing.Token, existing.DeviceName);
    }

    public async Task<ApiResponse<bool>> DeleteTokenAsync(Guid userId, string tokenId)
    {
        if (!Guid.TryParse(tokenId, out var id))
            return ApiResponse<bool>.Fail("Invalid token id.");

        var token = await _db.FcmTokens.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (token is null) return ApiResponse<bool>.Fail("Token not found.");

        _db.FcmTokens.Remove(token);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    private static NotificationDto Map(Notification n) => new(
        Id: n.Id.ToString(),
        Title: n.Title,
        Message: n.Message,
        Type: n.Type,
        SenderUsername: n.SenderUsername,
        SenderAvatar: n.SenderAvatar,
        Sender: n.SenderId?.ToString(),
        RelatedPostId: n.RelatedPostId?.ToString(),
        CreatedAt: n.CreatedAt.ToString("o"),
        IsRead: n.IsRead);
}
