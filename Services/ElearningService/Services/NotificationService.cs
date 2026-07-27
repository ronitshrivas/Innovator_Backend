using ElearningService.Common;
using ElearningService.Data;
using ElearningService.DTOs;
using ElearningService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ElearningService.Services;

public interface INotificationService
{
    Task<FcmTokenResponse> RegisterTokenAsync(Guid userId, FcmTokenRequest request);
    Task<ApiResponse<FcmTokenResponse>> UpdateTokenAsync(Guid userId, string tokenId, FcmTokenRequest request);
    Task<ApiResponse<bool>> DeleteTokenAsync(Guid userId, string tokenId);
    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId);
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, string notificationId);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId);
}

public class NotificationService : INotificationService
{
    private readonly ElearningDbContext _db;

    public NotificationService(ElearningDbContext db) => _db = db;

    public async Task<FcmTokenResponse> RegisterTokenAsync(Guid userId, FcmTokenRequest request)
    {
        var existing = await _db.FcmTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

        if (existing is null)
        {
            existing = new FcmToken
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

    public async Task<ApiResponse<FcmTokenResponse>> UpdateTokenAsync(Guid userId, string tokenId, FcmTokenRequest request)
    {
        if (!Guid.TryParse(tokenId, out var id))
            return ApiResponse<FcmTokenResponse>.Fail("Invalid token id.");

        var token = await _db.FcmTokens.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (token is null)
            return ApiResponse<FcmTokenResponse>.Fail("Token not found.");

        token.Token = request.Token;
        token.DeviceName = request.DeviceName;
        await _db.SaveChangesAsync();

        return ApiResponse<FcmTokenResponse>.Ok(
            new FcmTokenResponse(token.Id.ToString(), token.Token, token.DeviceName));
    }

    public async Task<ApiResponse<bool>> DeleteTokenAsync(Guid userId, string tokenId)
    {
        if (!Guid.TryParse(tokenId, out var id))
            return ApiResponse<bool>.Fail("Invalid token id.");

        var token = await _db.FcmTokens.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (token is null)
            return ApiResponse<bool>.Fail("Token not found.");

        _db.FcmTokens.Remove(token);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return notifications.Select(Map).ToList();
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, string notificationId)
    {
        if (!Guid.TryParse(notificationId, out var id))
            return ApiResponse<bool>.Fail("Invalid notification id.");

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null)
            return ApiResponse<bool>.Fail("Notification not found.");

        notification.IsRead = true;
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId)
    {
        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
            notification.IsRead = true;

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true);
    }

    private static NotificationDto Map(Notification n) => new(
        Id: n.Id.ToString(),
        Title: n.Title,
        Message: n.Message,
        NotificationType: n.NotificationType,
        IsRead: n.IsRead,
        CreatedAt: DateFormat.Iso(n.CreatedAt),
        Data: new NotificationDataDto(n.DataType, n.DataCourseId));
}
