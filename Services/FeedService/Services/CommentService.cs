using FeedService.Data;
using FeedService.DTOs;
using FeedService.Entities;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FeedService.Services;

public interface ICommentService
{
    Task<ApiResponse<List<CommentResponse>>> GetCommentsAsync(Guid postId, int page);
    Task<ApiResponse<List<CommentResponse>>> GetRepliesAsync(Guid parentId);
    Task<ApiResponse<CommentResponse>> AddCommentAsync(
        Guid postId, Guid authorId, string username,
        string? avatar, string content);
    Task<ApiResponse<CommentResponse>> AddReplyAsync(
        Guid parentId, Guid authorId, string username,
        string? avatar, string content);
    Task<ApiResponse<CommentResponse>> UpdateCommentAsync(
        Guid commentId, Guid requesterId, string content);
    Task<ApiResponse<bool>> DeleteCommentAsync(Guid commentId, Guid requesterId);
}

public class CommentBusinessService : ICommentService
{
    private readonly FeedDbContext _db;

    public CommentBusinessService(FeedDbContext db) => _db = db;

    public async Task<ApiResponse<List<CommentResponse>>> GetCommentsAsync(Guid postId, int page)
    {
        var skip = Math.Max(0, page - 1) * 20;

        var comments = await _db.Comments
            .Where(c => c.PostId == postId && c.ParentId == null)
            .Include(c => c.Replies)
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip).Take(20)
            .ToListAsync();

        return ApiResponse<List<CommentResponse>>.Ok(
            comments.Select(MapToResponse).ToList());
    }

    public async Task<ApiResponse<List<CommentResponse>>> GetRepliesAsync(Guid parentId)
    {
        var replies = await _db.Comments
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return ApiResponse<List<CommentResponse>>.Ok(
            replies.Select(MapToResponse).ToList());
    }

    public async Task<ApiResponse<CommentResponse>> AddCommentAsync(
        Guid postId, Guid authorId, string username,
        string? avatar, string content)
    {
        var comment = new Comment
        {
            PostId = postId,
            AuthorId = authorId,
            Username = username,
            Avatar = avatar,
            Content = content
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        return ApiResponse<CommentResponse>.Ok(MapToResponse(comment));
    }

    public async Task<ApiResponse<CommentResponse>> AddReplyAsync(
        Guid parentId, Guid authorId, string username,
        string? avatar, string content)
    {
        var parent = await _db.Comments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == parentId);

        if (parent == null)
            return ApiResponse<CommentResponse>.Fail("Comment not found.");

        var reply = new Comment
        {
            PostId = parent.PostId,
            ParentId = parentId,
            AuthorId = authorId,
            Username = username,
            Avatar = avatar,
            Content = content
        };

        _db.Comments.Add(reply);
        await _db.SaveChangesAsync();

        return ApiResponse<CommentResponse>.Ok(MapToResponse(reply));
    }

    public async Task<ApiResponse<CommentResponse>> UpdateCommentAsync(
        Guid commentId, Guid requesterId, string content)
    {
        var comment = await _db.Comments
            .Include(c => c.Replies)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
            return ApiResponse<CommentResponse>.Fail("Comment not found.");

        if (comment.AuthorId != requesterId)
            return ApiResponse<CommentResponse>.Fail("Not authorized.");

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<CommentResponse>.Ok(MapToResponse(comment));
    }

    public async Task<ApiResponse<bool>> DeleteCommentAsync(Guid commentId, Guid requesterId)
    {
        var comment = await _db.Comments.FindAsync(commentId);

        if (comment == null)
            return ApiResponse<bool>.Fail("Comment not found.");

        if (comment.AuthorId != requesterId)
            return ApiResponse<bool>.Fail("Not authorized.");

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true);
    }

    private static CommentResponse MapToResponse(Comment c) =>
        new(
            c.Id.ToString(),
            c.Username,
            c.Avatar,
            c.PostId?.ToString() ?? "",
            c.ParentId?.ToString(),
            c.Content,
            c.Replies.Count,
            c.CreatedAt
        );
}
