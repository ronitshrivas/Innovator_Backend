using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfileService.DTOs;
using ProfileService.Services;
using System.Security.Claims;

namespace ProfileService.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public SettingsController(ISettingsService settings) => _settings = settings;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _settings.GetAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request)
    {
        var result = await _settings.UpdateAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        var result = await _settings.ResetAsync(CurrentUserId);
        return Ok(result);
    }
}

// Service-to-service preference flags for Feed/Chat/Search enforcement.
[ApiController]
[Route("api/internal/settings")]
[AllowAnonymous]
public class InternalSettingsController : ControllerBase
{
    private readonly ISettingsService _settings;

    public InternalSettingsController(ISettingsService settings) => _settings = settings;

    [HttpPost("batch")]
    public async Task<IActionResult> Batch([FromBody] SettingsFlagsRequest request)
    {
        var ids = new List<Guid>();
        foreach (var s in request.UserIds ?? new())
            if (Guid.TryParse(s, out var g)) ids.Add(g);

        var flags = await _settings.GetFlagsAsync(ids);
        return Ok(flags);
    }
}
