using System.Globalization;
using EventsService.Data;
using EventsService.DTOs;
using EventsService.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsService.Services;

public interface IEventService
{
    Task<List<EventDto>> GetEventsAsync(Guid? currentUserId);
    Task<EventDto> CreateEventAsync(Guid userId, string username, CreateEventRequest request);
}

public class EventService : IEventService
{
    private readonly EventsDbContext _db;

    public EventService(EventsDbContext db) => _db = db;

    public async Task<List<EventDto>> GetEventsAsync(Guid? currentUserId)
    {
        var events = await _db.Events
            .Include(e => e.Participants)
            .OrderBy(e => e.Date)
            .AsNoTracking()
            .ToListAsync();

        return events.Select(e => Map(e, currentUserId)).ToList();
    }

    public async Task<EventDto> CreateEventAsync(Guid userId, string username, CreateEventRequest request)
    {
        var newEvent = new Event
        {
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            Date = ParseDate(request.Date),
            CreatedById = userId,
            CreatedByUsername = username
        };

        _db.Events.Add(newEvent);
        await _db.SaveChangesAsync();

        return Map(newEvent, userId);
    }

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTime.UtcNow;

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }

    private static EventDto Map(Event e, Guid? currentUserId) => new(
        Id: e.Id.ToString(),
        Title: e.Title,
        Description: e.Description,
        Location: e.Location,
        Date: Iso(e.Date),
        CreatedByUsername: e.CreatedByUsername,
        ParticipantsCount: e.Participants.Count,
        IsParticipant: currentUserId is not null && e.Participants.Any(p => p.UserId == currentUserId),
        CreatedAt: Iso(e.CreatedAt));

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
