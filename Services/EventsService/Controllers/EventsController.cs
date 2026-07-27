using System.Security.Claims;
using EventsService.DTOs;
using EventsService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService) => _eventService = eventService;

    private Guid? CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    private string CurrentUsername =>
        User.FindFirstValue("username")
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "unknown";

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetEvents()
    {
        var events = await _eventService.GetEventsAsync(CurrentUserId);
        return Ok(events);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var userId = CurrentUserId ?? throw new InvalidOperationException("Missing user id claim.");
        var created = await _eventService.CreateEventAsync(userId, CurrentUsername, request);
        return StatusCode(201, created);
    }
}
