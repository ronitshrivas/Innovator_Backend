using ChatService.DTOs;
using ChatService.Services;
using ChatService.WebSockets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatService.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService) => _chatService = chatService;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private string CurrentUsername => User.FindFirstValue("username") ?? string.Empty;

    [HttpPost("conversations")]
    public async Task<IActionResult> GetOrCreateConversation(
        [FromBody] CreateConversationRequest request)
    {
        var result = await _chatService.GetOrCreateConversationAsync(
            CurrentUserId, CurrentUsername, null, request);
        if (result.Success) return Ok(result);
        // who_can_message denials surface as 403.
        return result.Message.Contains("message", StringComparison.OrdinalIgnoreCase)
               && (result.Message.Contains("doesn't accept") || result.Message.Contains("followers"))
            ? StatusCode(403, result)
            : BadRequest(result);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetMyConversations()
    {
        var result = await _chatService.GetMyConversationsAsync(CurrentUserId);
        return Ok(result);
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(Guid conversationId)
    {
        var result = await _chatService.GetConversationAsync(conversationId, CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid conversationId)
    {
        var result = await _chatService.DeleteConversationAsync(conversationId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId, [FromQuery] int page = 1)
    {
        var result = await _chatService.GetMessagesAsync(conversationId, CurrentUserId, page);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId, [FromBody] SendMessageRequest request)
    {
        var result = await _chatService.SendMessageAsync(
            conversationId, CurrentUserId, CurrentUsername, null, request);

        if (result.Success && result.Data != null)
        {
            var conversation = await _chatService.GetConversationAsync(
                conversationId, CurrentUserId);

            if (conversation.Success)
            {
                var participantIds = conversation.Data!.Participants
                    .Select(p => Guid.Parse(p.UserId)).ToList();

                await ChatWebSocketHandler.BroadcastToConversationAsync(
                    participantIds, "new_message", result.Data);
            }
        }

        return result.Success ? StatusCode(201, result) : BadRequest(result);
    }

    [HttpPatch("messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(
        Guid messageId, [FromBody] UpdateMessageRequest request)
    {
        var result = await _chatService.UpdateMessageAsync(
            messageId, CurrentUserId, request.Content);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        var result = await _chatService.DeleteMessageAsync(messageId, CurrentUserId);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var result = await _chatService.MarkAsReadAsync(conversationId, CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
