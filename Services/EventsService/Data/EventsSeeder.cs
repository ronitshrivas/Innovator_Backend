using EventsService.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventsService.Data;

/// <summary>
/// Inserts a couple of upcoming events on first run so the events screen is
/// not empty. No-ops once events already exist.
/// </summary>
public static class EventsSeeder
{
    public static async Task SeedAsync(EventsDbContext db)
    {
        if (await db.Events.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        db.Events.AddRange(
            new Event
            {
                Title = "Flutter Nepal Meetup",
                Description = "A community meetup for Flutter developers with talks and networking.",
                Location = "Kathmandu, Nepal",
                Date = now.AddDays(14),
                CreatedByUsername = "innovator"
            },
            new Event
            {
                Title = ".NET Backend Workshop",
                Description = "Hands-on session on building microservices with ASP.NET Core.",
                Location = "Online",
                Date = now.AddDays(30),
                CreatedByUsername = "innovator"
            });

        await db.SaveChangesAsync();
    }
}
