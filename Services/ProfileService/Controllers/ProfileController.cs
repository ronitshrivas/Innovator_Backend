using Innovator.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfileService.DTOs;
using ProfileService.Services;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace ProfileService.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpGet("users/me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), 200)]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _profileService.GetMyProfileAsync(CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("profile")]
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var result = await _profileService.UpdateProfileAsync(CurrentUserId, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/me/avatar")]
    [HttpPost("users/me/avatar")]
    public async Task<IActionResult> UpdateAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var result = await _profileService.UpdateAvatarAsync(CurrentUserId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("users/me/cover")]
    [HttpPost("users/me/cover")]
    public async Task<IActionResult> UpdateCover(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        var result = await _profileService.UpdateCoverAsync(CurrentUserId, file);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("users/me/cover")]
    public async Task<IActionResult> DeleteCover()
    {
        var result = await _profileService.DeleteCoverAsync(CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet("users/{authUserId:guid}")]
    public async Task<IActionResult> GetUserById(Guid authUserId)
    {
        var result = await _profileService.GetProfileByAuthIdAsync(authUserId, CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("users/{username}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserByUsername(string username)
    {
        var result = await _profileService.GetProfileByUsernameAsync(username, CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("users/followers")]
    public async Task<IActionResult> GetMyFollowers()
    {
        var result = await _profileService.GetFollowersAsync(CurrentUserId, CurrentUserId);
        return Ok(result);
    }

    [HttpGet("users/following")]
    public async Task<IActionResult> GetMyFollowing()
    {
        var result = await _profileService.GetFollowingAsync(CurrentUserId, CurrentUserId);
        return Ok(result);
    }

    [HttpGet("users/{authUserId:guid}/followers")]
    public async Task<IActionResult> GetUserFollowers(Guid authUserId)
    {
        var result = await _profileService.GetFollowersAsync(authUserId, CurrentUserId);
        return Ok(result);
    }

    [HttpGet("users/{authUserId:guid}/following")]
    public async Task<IActionResult> GetUserFollowing(Guid authUserId)
    {
        var result = await _profileService.GetFollowingAsync(authUserId, CurrentUserId);
        return Ok(result);
    }

    [HttpPost("users/{targetAuthUserId:guid}/follow")]
    public async Task<IActionResult> ToggleFollow(Guid targetAuthUserId)
    {
        var result = await _profileService.ToggleFollowAsync(CurrentUserId, targetAuthUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // "Suggested for you" — people the viewer should follow.
    [HttpGet("users/suggested")]
    public async Task<IActionResult> GetSuggested([FromQuery] int limit = 10)
    {
        var result = await _profileService.GetSuggestedUsersAsync(CurrentUserId, limit);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // "Find friends" — paginated, searchable people directory to connect with.
    // Each entry carries avatar, username, name, a headline (occupation, or
    // education when no occupation), and the viewer's follow status.
    [HttpGet("users/find-friends")]
    public async Task<IActionResult> FindFriends(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _profileService.FindFriendsAsync(
            CurrentUserId, query, page, pageSize);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/suggested/{dismissedAuthUserId:guid}/dismiss")]
    public async Task<IActionResult> DismissSuggestion(Guid dismissedAuthUserId)
    {
        var result = await _profileService.DismissSuggestionAsync(CurrentUserId, dismissedAuthUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Pending follow requests waiting for the current (private) user to approve.
    [HttpGet("users/follow-requests")]
    public async Task<IActionResult> GetFollowRequests()
    {
        var result = await _profileService.GetFollowRequestsAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/follow-requests/{requesterAuthUserId:guid}/accept")]
    public async Task<IActionResult> AcceptFollowRequest(Guid requesterAuthUserId)
    {
        var result = await _profileService.RespondToFollowRequestAsync(
            CurrentUserId, requesterAuthUserId, accept: true);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/follow-requests/{requesterAuthUserId:guid}/reject")]
    public async Task<IActionResult> RejectFollowRequest(Guid requesterAuthUserId)
    {
        var result = await _profileService.RespondToFollowRequestAsync(
            CurrentUserId, requesterAuthUserId, accept: false);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("users/{targetAuthUserId:guid}/block")]
    public async Task<IActionResult> ToggleBlock(Guid targetAuthUserId)
    {
        var result = await _profileService.ToggleBlockAsync(CurrentUserId, targetAuthUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("users/blocked-list")]
    public async Task<IActionResult> GetBlockedList()
    {
        var result = await _profileService.GetBlockedListAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpPost("users/{targetAuthUserId:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid targetAuthUserId)
    {
        var result = await _profileService.ToggleBlockAsync(CurrentUserId, targetAuthUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

[ApiController]
[Route("api/internal")]
public class InternalProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public InternalProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("profiles/ensure")]
    public async Task<IActionResult> EnsureProfile([FromBody] EnsureProfileRequest request)
    {
        await _profileService.EnsureProfileExistsAsync(
            request.AuthUserId, request.Username, request.Email, request.Role);
        return Ok();
    }

    // Auth ids the user has blocked or been blocked by (for search exclusion).
    [HttpGet("profiles/{authUserId:guid}/block-pairs")]
    public async Task<IActionResult> GetBlockPairs(Guid authUserId)
    {
        var ids = await _profileService.GetBlockPairIdsAsync(authUserId);
        return Ok(ids);
    }

    // 1st + 2nd degree follow graph for feed candidate generation.
    [HttpGet("profiles/{authUserId:guid}/follow-graph")]
    public async Task<IActionResult> GetFollowGraph(Guid authUserId)
    {
        var graph = await _profileService.GetFollowGraphAsync(authUserId);
        return Ok(graph);
    }

    // Batch avatar lookup so the feed can show each author's current avatar.
    [HttpPost("profiles/avatars")]
    public async Task<IActionResult> GetAvatars([FromBody] AvatarLookupRequest request)
    {
        var ids = new List<Guid>();
        foreach (var s in request.AuthUserIds ?? new())
            if (Guid.TryParse(s, out var g)) ids.Add(g);

        var map = await _profileService.GetAvatarsAsync(ids);
        return Ok(map);
    }

    // Batch author info (avatar + occupation + is_followed) for the feed.
    [HttpPost("profiles/author-info")]
    public async Task<IActionResult> GetAuthorInfo([FromBody] AuthorInfoLookupRequest request)
    {
        // Accept ids/requester under either snake_case or camelCase so a caller's
        // JSON naming policy can never silently break this lookup.
        var rawIds = request.AuthUserIds ?? request.AuthUserIdsCamel ?? new List<string>();
        var rawRequester = request.RequesterId ?? request.RequesterIdCamel;

        var ids = new List<Guid>();
        foreach (var s in rawIds)
            if (Guid.TryParse(s, out var g)) ids.Add(g);

        Guid? requester = Guid.TryParse(rawRequester, out var r) ? r : null;
        var map = await _profileService.GetAuthorInfoAsync(ids, requester);
        return Ok(map);
    }

    public record EnsureProfileRequest(Guid AuthUserId, string Username, string Email, string Role);
    public record AvatarLookupRequest(List<string>? AuthUserIds);

    public record AuthorInfoLookupRequest(
        [property: JsonPropertyName("auth_user_ids")] List<string>? AuthUserIds,
        [property: JsonPropertyName("requester_id")] string? RequesterId,
        [property: JsonPropertyName("authUserIds")] List<string>? AuthUserIdsCamel = null,
        [property: JsonPropertyName("requesterId")] string? RequesterIdCamel = null);
}
