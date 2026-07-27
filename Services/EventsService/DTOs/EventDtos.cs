using System.ComponentModel.DataAnnotations;

namespace EventsService.DTOs;

public record EventDto(
    string Id,
    string Title,
    string Description,
    string Location,
    string Date,
    string CreatedByUsername,
    int ParticipantsCount,
    bool IsParticipant,
    string CreatedAt
);

public record CreateEventRequest(
    [Required] string Title,
    string Description = "",
    string Location = "",
    string Date = "",
    List<string>? Participants = null
);
