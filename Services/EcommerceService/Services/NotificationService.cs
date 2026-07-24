using System.Text.Json;
using EcommerceService.Data;
using EcommerceService.DTOs;
using EcommerceService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EcommerceService.Services;

public interface INotificationService
{
    Task<ApiResponse<bool>> RegisterFcmTokenAsync(Guid userId, FcmTokenRequest request);
    Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync(Guid userId);
    Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId);
}

public class NotificationService : INotificationService
{
    private readonly EcommerceDbContext _db;

    public NotificationService(EcommerceDbContext db) => _db = db;

    public async Task<ApiResponse<bool>> RegisterFcmTokenAsync(
        Guid userId, FcmTokenRequest request)
    {
        var existing = await _db.FcmTokens
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Token == request.Token);

        if (existing == null)
        {
            _db.FcmTokens.Add(new FcmToken
            {
                UserId = userId,
                Token = request.Token,
                Platform = request.Platform
            });
            await _db.SaveChangesAsync();
        }

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<List<NotificationDto>>> GetNotificationsAsync(Guid userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        return ApiResponse<List<NotificationDto>>.Ok(
            notifications.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return ApiResponse<bool>.Fail("Notification not found.");

        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(Guid userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(n => n.IsRead, true)
                 .SetProperty(n => n.UpdatedAt, DateTime.UtcNow));

        return ApiResponse<bool>.Ok(true);
    }

    private static NotificationDto MapToDto(EcommerceNotification n)
    {
        var data = new NotificationDataDto("", "", "");
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(n.DataJson);
            if (parsed != null)
            {
                data = new NotificationDataDto(
                    parsed.GetValueOrDefault("type", ""),
                    parsed.GetValueOrDefault("product_id", ""),
                    parsed.GetValueOrDefault("category", ""));
            }
        }
        catch { }

        return new NotificationDto(
            n.Id.ToString(),
            n.Title,
            n.Message,
            n.NotificationType,
            n.IsRead,
            n.CreatedAt.ToString("O"),
            data);
    }
}
