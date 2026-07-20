using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChatService.Services;
using Innovator.Shared.Helpers;

namespace ChatService.WebSockets;

public class ChatWebSocketHandler
{
    private static readonly ConcurrentDictionary<Guid, WebSocket> ConnectedUsers = new();

    private readonly IChatService _chatService;
    private readonly IConfiguration _config;
    private readonly ILogger<ChatWebSocketHandler> _logger;

    public ChatWebSocketHandler(
        IChatService chatService,
        IConfiguration config,
        ILogger<ChatWebSocketHandler> logger)
    {
        _chatService = chatService;
        _config = config;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var token = context.Request.Query["token"].ToString();
        var principal = JwtHelper.ValidateToken(token, _config["Jwt:Secret"]!);

        if (principal == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        var userIdStr =
            principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        ConnectedUsers[userId] = socket;

        _logger.LogInformation("User {UserId} connected via WebSocket", userId);

        try
        {
            await ListenAsync(socket, userId);
        }
        finally
        {
            ConnectedUsers.TryRemove(userId, out _);
            _logger.LogInformation("User {UserId} disconnected", userId);
        }
    }

    private async Task ListenAsync(WebSocket socket, Guid userId)
    {
        var buffer = new byte[4096];

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                break;
            }

            var raw = Encoding.UTF8.GetString(buffer, 0, result.Count);

            try
            {
                var envelope = JsonSerializer.Deserialize<JsonElement>(raw);
                var eventType = envelope.GetProperty("event").GetString();

                if (eventType == "ping")
                    await SendToUserAsync(userId, "pong", new { });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse WebSocket message from {UserId}", userId);
            }
        }
    }

    public static async Task BroadcastToConversationAsync(
        List<Guid> participantIds, string eventType, object payload)
    {
        var json = JsonSerializer.Serialize(new { @event = eventType, data = payload });
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        foreach (var participantId in participantIds)
        {
            if (ConnectedUsers.TryGetValue(participantId, out var socket) &&
                socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(
                    segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private static async Task SendToUserAsync(Guid userId, string eventType, object payload)
    {
        if (!ConnectedUsers.TryGetValue(userId, out var socket) ||
            socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(new { @event = eventType, data = payload });
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
