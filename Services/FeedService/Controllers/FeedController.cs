using FeedService.DTOs;
using FeedService.Services;
using Innovator.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeedService.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService) => _feedService = feedService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private string CurrentUsername => User.FindFirstValue("username") ?? string.Empty;

    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _feedService.GetFeedAsync(CurrentUserId, page, pageSize);
        return Ok(result);
    }

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost(
        [FromForm] string? content,
        [FromForm] List<string>? categoryIds,
        [FromForm] string? sharedPostId,
        [FromForm] List<IFormFile>? media)
    {
        // A post needs text, media, or be a repost (sharedPostId).
        if (string.IsNullOrWhiteSpace(content)
            && (media == null || media.Count == 0)
            && string.IsNullOrWhiteSpace(sharedPostId))
            return BadRequest(new { message = "A post needs text, media, or a shared post." });

        var request = new CreatePostRequest(content ?? string.Empty, categoryIds, sharedPostId);
        var result = await _feedService.CreatePostAsync(
            CurrentUserId, CurrentUsername, string.Empty, request, media);
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpGet("posts/{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPost(Guid postId)
    {
        var result = await _feedService.GetPostAsync(postId, CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPatch("posts/{postId:guid}")]
    public async Task<IActionResult> UpdatePost(
        Guid postId,
        [FromForm] string content,
        [FromForm] IFormFile? uploadedMedia)
    {
        var result = await _feedService.UpdatePostAsync(
            postId, CurrentUserId, content, uploadedMedia);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("posts/{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId)
    {
        var result = await _feedService.DeletePostAsync(postId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("posts/{postId:guid}/view")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordView(Guid postId)
    {
        var result = await _feedService.RecordViewAsync(postId);
        return Ok(result);
    }

    // Who reposted this post (direct reposts and reposts-with-thought).
    [HttpGet("posts/{postId:guid}/reposts")]
    public async Task<IActionResult> GetReposts(
        Guid postId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _feedService.GetRepostsAsync(postId, CurrentUserId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _feedService.GetCategoriesAsync();
        return Ok(result);
    }

    [HttpGet("reels")]
    public async Task<IActionResult> GetReels(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _feedService.GetReelFeedAsync(CurrentUserId, page, pageSize);
        return Ok(result);
    }

    [HttpPost("reels")]
    public async Task<IActionResult> CreateReel(
        [FromForm] string caption,
        [FromForm] IFormFile video)
    {
        var result = await _feedService.CreateReelAsync(
            CurrentUserId, CurrentUsername, string.Empty, caption, video);
        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPatch("reels/{reelId:guid}")]
    public async Task<IActionResult> UpdateReel(Guid reelId, [FromBody] UpdatePostRequest request)
    {
        var result = await _feedService.UpdatePostAsync(
            reelId, CurrentUserId, request.Content, null);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("reels/{reelId:guid}")]
    public async Task<IActionResult> DeleteReel(Guid reelId)
    {
        var result = await _feedService.DeletePostAsync(reelId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet("users/{authorId:guid}/posts")]
    public async Task<IActionResult> GetUserPosts(
        Guid authorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _feedService.GetUserPostsAsync(authorId, CurrentUserId, page, pageSize);
        return Ok(result);
    }
}

[ApiController]
[Route("api/reactions")]
[Authorize]
public class ReactionController : ControllerBase
{
    private readonly IReactionService _reactionService;

    public ReactionController(IReactionService reactionService) =>
        _reactionService = reactionService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    [HttpPost]
    public async Task<IActionResult> React([FromBody] CreateReactionRequest request)
    {
        if (!Guid.TryParse(request.Post, out var postId))
            return BadRequest(new { message = "Invalid post id." });

        var result = await _reactionService.ToggleReactionAsync(postId, CurrentUserId, request.Type);

        if (result.Data == null)
            return StatusCode(204);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("posts/{postId:guid}")]
    public async Task<IActionResult> GetReactions(Guid postId)
    {
        var result = await _reactionService.GetReactionsAsync(postId);
        return Ok(result);
    }
}

[ApiController]
[Route("api/comments")]
[Authorize]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService) =>
        _commentService = commentService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private string CurrentUsername => User.FindFirstValue("username") ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetComments(
        [FromQuery] string? post,
        [FromQuery] string? reel,
        [FromQuery] int page = 1)
    {
        var idStr = post ?? reel;
        if (!Guid.TryParse(idStr, out var postId))
            return BadRequest(new { message = "post or reel query param is required." });

        var result = await _commentService.GetCommentsAsync(postId, page);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddComment([FromBody] CreateCommentRequest request)
    {
        var idStr = request.Post ?? request.Reel;
        if (!Guid.TryParse(idStr, out var postId))
            return BadRequest(new { message = "post or reel is required." });

        var result = await _commentService.AddCommentAsync(
            postId, CurrentUserId, CurrentUsername, null, request.Content);

        if (result.Success) return StatusCode(201, result);
        // Permission denials from who_can_comment surface as 403.
        return result.Message.Contains("comment", StringComparison.OrdinalIgnoreCase)
               && (result.Message.Contains("allow") || result.Message.Contains("followers"))
            ? StatusCode(403, result)
            : BadRequest(result);
    }

    [HttpPatch("{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(
        Guid commentId,
        [FromBody] UpdateCommentRequest request)
    {
        var result = await _commentService.UpdateCommentAsync(
            commentId, CurrentUserId, request.Content);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        var result = await _commentService.DeleteCommentAsync(commentId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }
}

[ApiController]
[Route("api/replies")]
[Authorize]
public class ReplyController : ControllerBase
{
    private readonly ICommentService _commentService;

    public ReplyController(ICommentService commentService) =>
        _commentService = commentService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private string CurrentUsername => User.FindFirstValue("username") ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetReplies([FromQuery] Guid parent)
    {
        var result = await _commentService.GetRepliesAsync(parent);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddReply([FromBody] CreateReplyRequest request)
    {
        if (!Guid.TryParse(request.Parent, out var parentId))
            return BadRequest(new { message = "Invalid parent id." });

        var result = await _commentService.AddReplyAsync(
            parentId, CurrentUserId, CurrentUsername, null, request.Content);

        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPatch("{replyId:guid}")]
    public async Task<IActionResult> UpdateReply(
        Guid replyId,
        [FromBody] UpdateCommentRequest request)
    {
        var result = await _commentService.UpdateCommentAsync(
            replyId, CurrentUserId, request.Content);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{replyId:guid}")]
    public async Task<IActionResult> DeleteReply(Guid replyId)
    {
        var result = await _commentService.DeleteCommentAsync(replyId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }
}
