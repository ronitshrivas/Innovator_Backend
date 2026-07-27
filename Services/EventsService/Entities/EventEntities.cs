using Innovator.Shared.Entities;

namespace EventsService.Entities;

public class Event : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public Guid CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;

    public List<EventParticipant> Participants { get; set; } = new();
}

public class EventParticipant : BaseEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
}
