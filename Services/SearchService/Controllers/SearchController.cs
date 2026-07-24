using Innovator.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SearchService.DTOs;
using SearchService.Services;
using System.Security.Claims;

namespace SearchService.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService) =>
        _searchService = searchService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string type = "all")
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Query param q is required." });

        var result = await _searchService.SearchAsync(q, CurrentUserId, type);
        return Ok(result);
    }

    [HttpGet("search/users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Query param q is required." });

        var result = await _searchService.SearchUsersAsync(q, CurrentUserId);
        return Ok(result);
    }

    [HttpGet("search/posts")]
    public async Task<IActionResult> SearchPosts([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Query param q is required." });

        var result = await _searchService.SearchPostsAsync(q);
        return Ok(result);
    }

    [HttpGet("search/hashtags")]
    public async Task<IActionResult> SearchHashtags([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Query param q is required." });

        var result = await _searchService.SearchHashtagsAsync(q);
        return Ok(result);
    }

    [HttpGet("suggested-users")]
    [HttpGet("users/suggested")]
    public async Task<IActionResult> GetSuggestedUsers()
    {
        var result = await _searchService.GetSuggestedUsersAsync(CurrentUserId);

        if (result.Data == null) return Ok(result);

        return Ok(result.Data.Suggestions);
    }

    [HttpGet("search/history")]
    public async Task<IActionResult> GetSearchHistory()
    {
        var result = await _searchService.GetSearchHistoryAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpDelete("search/history")]
    public async Task<IActionResult> ClearSearchHistory()
    {
        var result = await _searchService.ClearSearchHistoryAsync(CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }
}

[ApiController]
[Route("api/internal/search")]
public class IndexSyncController : ControllerBase
{
    private readonly IIndexSyncService _indexSync;

    public IndexSyncController(IIndexSyncService indexSync) =>
        _indexSync = indexSync;

    [HttpPost("users")]
    public async Task<IActionResult> UpsertUser([FromBody] UpsertUserIndexRequest request)
    {
        var result = await _indexSync.UpsertUserAsync(request);
        return Ok(result);
    }

    [HttpDelete("users/{authUserId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid authUserId)
    {
        var result = await _indexSync.DeleteUserAsync(authUserId);
        return Ok(result);
    }

    [HttpPost("posts")]
    public async Task<IActionResult> UpsertPost([FromBody] UpsertPostIndexRequest request)
    {
        var result = await _indexSync.UpsertPostAsync(request);
        return Ok(result);
    }

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId)
    {
        var result = await _indexSync.DeletePostAsync(postId);
        return Ok(result);
    }

    [HttpPost("follows")]
    public async Task<IActionResult> SyncFollow([FromBody] SyncFollowRequest request)
    {
        var result = await _indexSync.SyncFollowAsync(request);
        return Ok(result);
    }
}
