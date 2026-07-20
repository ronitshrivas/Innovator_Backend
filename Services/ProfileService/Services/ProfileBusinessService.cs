using System.Text.Json;
using Innovator.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using ProfileService.Data;
using ProfileService.DTOs;
using ProfileService.Entities;

namespace ProfileService.Services;

public interface IProfileService
{
    Task<ApiResponse<ProfileResponse>> GetMyProfileAsync(Guid authUserId);
    Task<ApiResponse<ProfileResponse>> GetProfileByUsernameAsync(string username, Guid? requesterId);
    Task<ApiResponse<ProfileResponse>> GetProfileByAuthIdAsync(Guid targetAuthUserId, Guid? requesterId);
    Task<ApiResponse<ProfileResponse>> UpdateProfileAsync(Guid authUserId, UpdateProfileRequest request);
    Task<ApiResponse<string>> UpdateAvatarAsync(Guid authUserId, IFormFile file);
    Task<ApiResponse<FollowActionResponse>> ToggleFollowAsync(Guid followerId, Guid targetAuthUserId);
    Task<ApiResponse<List<UserSummaryDto>>> GetFollowersAsync(Guid authUserId, Guid requesterId);
    Task<ApiResponse<List<UserSummaryDto>>> GetFollowingAsync(Guid authUserId, Guid requesterId);
    Task<ApiResponse<BlockActionResponse>> ToggleBlockAsync(Guid blockerId, Guid targetAuthUserId);
    Task<ApiResponse<List<BlockedUserDto>>> GetBlockedListAsync(Guid authUserId);
    Task EnsureProfileExistsAsync(Guid authUserId, string username, string email, string role);
}

public class ProfileBusinessService : IProfileService
{
    private readonly ProfileDbContext _db;
    private readonly IAvatarStorageService _avatarStorage;

    public ProfileBusinessService(ProfileDbContext db, IAvatarStorageService avatarStorage)
    {
        _db = db;
        _avatarStorage = avatarStorage;
    }

    public async Task<ApiResponse<ProfileResponse>> GetMyProfileAsync(Guid authUserId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Followers)
            .Include(p => p.Following)
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);

        if (profile == null)
            return ApiResponse<ProfileResponse>.Fail("Profile not found.");

        return ApiResponse<ProfileResponse>.Ok(MapToResponse(profile, false));
    }

    public async Task<ApiResponse<ProfileResponse>> GetProfileByUsernameAsync(string username, Guid? requesterId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Followers)
            .Include(p => p.Following)
            .FirstOrDefaultAsync(p => p.Username == username.ToLower());

        if (profile == null)
            return ApiResponse<ProfileResponse>.Fail("User not found.");

        bool isFollowed = requesterId.HasValue && profile.Followers
            .Any(f => f.FollowerId == requesterId.Value && f.Status == FollowStatus.Accepted);

        return ApiResponse<ProfileResponse>.Ok(MapToResponse(profile, isFollowed));
    }

    public async Task<ApiResponse<ProfileResponse>> GetProfileByAuthIdAsync(Guid targetAuthUserId, Guid? requesterId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Followers)
            .Include(p => p.Following)
            .FirstOrDefaultAsync(p => p.AuthUserId == targetAuthUserId);

        if (profile == null)
            return ApiResponse<ProfileResponse>.Fail("User not found.");

        bool isFollowed = requesterId.HasValue && profile.Followers
            .Any(f => f.FollowerId == requesterId.Value && f.Status == FollowStatus.Accepted);

        return ApiResponse<ProfileResponse>.Ok(MapToResponse(profile, isFollowed));
    }

    public async Task<ApiResponse<ProfileResponse>> UpdateProfileAsync(Guid authUserId, UpdateProfileRequest request)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Followers)
            .Include(p => p.Following)
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);

        if (profile == null)
            return ApiResponse<ProfileResponse>.Fail("Profile not found.");

        if (request.FullName is not null) profile.FullName = request.FullName;
        if (request.Bio is not null) profile.Bio = request.Bio;
        if (request.DateOfBirth is not null) profile.DateOfBirth = request.DateOfBirth;
        if (request.Phone is not null) profile.Phone = request.Phone;
        if (request.Gender is not null) profile.Gender = request.Gender;
        if (request.Address is not null) profile.Address = request.Address;
        if (request.Education is not null) profile.Education = request.Education;
        if (request.Occupation is not null) profile.Occupation = request.Occupation;
        if (request.Interests is not null)
            profile.InterestsJson = JsonSerializer.Serialize(request.Interests);

        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<ProfileResponse>.Ok(MapToResponse(profile, false), "Profile updated.");
    }

    public async Task<ApiResponse<string>> UpdateAvatarAsync(Guid authUserId, IFormFile file)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (profile == null)
            return ApiResponse<string>.Fail("Profile not found.");

        _avatarStorage.DeleteAvatar(profile.AvatarPath);

        var relativePath = await _avatarStorage.SaveAvatarAsync(file, profile.Username);
        profile.AvatarPath = relativePath;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var publicUrl = _avatarStorage.ResolvePublicUrl(relativePath);
        return ApiResponse<string>.Ok(publicUrl, "Avatar updated.");
    }

    public async Task<ApiResponse<FollowActionResponse>> ToggleFollowAsync(Guid followerAuthId, Guid targetAuthUserId)
    {
        if (followerAuthId == targetAuthUserId)
            return ApiResponse<FollowActionResponse>.Fail("You cannot follow yourself.");

        var followerProfile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == followerAuthId);
        var targetProfile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == targetAuthUserId);

        if (followerProfile == null || targetProfile == null)
            return ApiResponse<FollowActionResponse>.Fail("User not found.");

        var existing = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowerId == followerProfile.Id && f.FollowingId == targetProfile.Id);

        if (existing != null)
        {
            _db.Follows.Remove(existing);
            await _db.SaveChangesAsync();
            return ApiResponse<FollowActionResponse>.Ok(new FollowActionResponse(false, "Unfollowed."));
        }

        _db.Follows.Add(new Follow
        {
            FollowerId = followerProfile.Id,
            FollowingId = targetProfile.Id,
            Status = FollowStatus.Accepted
        });

        await _db.SaveChangesAsync();
        return ApiResponse<FollowActionResponse>.Ok(new FollowActionResponse(true, "Following."));
    }

    public async Task<ApiResponse<List<UserSummaryDto>>> GetFollowersAsync(Guid authUserId, Guid requesterId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Followers).ThenInclude(f => f.Follower)
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);

        if (profile == null)
            return ApiResponse<List<UserSummaryDto>>.Fail("User not found.");

        var requesterFollowing = await _db.Follows
            .Where(f => f.Follower.AuthUserId == requesterId)
            .Select(f => f.FollowingId)
            .ToListAsync();

        var followers = profile.Followers
            .Where(f => f.Status == FollowStatus.Accepted)
            .Select(f => new UserSummaryDto(
                f.Follower.AuthUserId,
                f.Follower.Username,
                f.Follower.FullName,
                _avatarStorage.ResolvePublicUrl(f.Follower.AvatarPath),
                f.Follower.Role,
                requesterFollowing.Contains(f.Follower.Id)))
            .ToList();

        return ApiResponse<List<UserSummaryDto>>.Ok(followers);
    }

    public async Task<ApiResponse<List<UserSummaryDto>>> GetFollowingAsync(Guid authUserId, Guid requesterId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.Following).ThenInclude(f => f.FollowingUser)
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);

        if (profile == null)
            return ApiResponse<List<UserSummaryDto>>.Fail("User not found.");

        var requesterFollowing = await _db.Follows
            .Where(f => f.Follower.AuthUserId == requesterId)
            .Select(f => f.FollowingId)
            .ToListAsync();

        var following = profile.Following
            .Where(f => f.Status == FollowStatus.Accepted)
            .Select(f => new UserSummaryDto(
                f.FollowingUser.AuthUserId,
                f.FollowingUser.Username,
                f.FollowingUser.FullName,
                _avatarStorage.ResolvePublicUrl(f.FollowingUser.AvatarPath),
                f.FollowingUser.Role,
                requesterFollowing.Contains(f.FollowingUser.Id)))
            .ToList();

        return ApiResponse<List<UserSummaryDto>>.Ok(following);
    }

    public async Task<ApiResponse<BlockActionResponse>> ToggleBlockAsync(Guid blockerAuthId, Guid targetAuthUserId)
    {
        var blocker = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == blockerAuthId);
        var target = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == targetAuthUserId);

        if (blocker == null || target == null)
            return ApiResponse<BlockActionResponse>.Fail("User not found.");

        var existing = await _db.BlockedUsers
            .FirstOrDefaultAsync(b => b.BlockerId == blocker.Id && b.BlockedId == target.Id);

        if (existing != null)
        {
            _db.BlockedUsers.Remove(existing);
            await _db.SaveChangesAsync();
            return ApiResponse<BlockActionResponse>.Ok(new BlockActionResponse(false, "Unblocked."));
        }

        var follow = await _db.Follows
            .FirstOrDefaultAsync(f =>
                (f.FollowerId == blocker.Id && f.FollowingId == target.Id) ||
                (f.FollowerId == target.Id && f.FollowingId == blocker.Id));

        if (follow != null)
            _db.Follows.Remove(follow);

        _db.BlockedUsers.Add(new BlockedUser
        {
            BlockerId = blocker.Id,
            BlockedId = target.Id
        });

        await _db.SaveChangesAsync();
        return ApiResponse<BlockActionResponse>.Ok(new BlockActionResponse(true, "Blocked."));
    }

    public async Task<ApiResponse<List<BlockedUserDto>>> GetBlockedListAsync(Guid authUserId)
    {
        var profile = await _db.UserProfiles
            .Include(p => p.BlockedUsers).ThenInclude(b => b.Blocked)
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);

        if (profile == null)
            return ApiResponse<List<BlockedUserDto>>.Fail("Profile not found.");

        var list = profile.BlockedUsers.Select(b => new BlockedUserDto(
            b.Blocked.AuthUserId,
            b.Blocked.Username,
            b.Blocked.FullName,
            _avatarStorage.ResolvePublicUrl(b.Blocked.AvatarPath))).ToList();

        return ApiResponse<List<BlockedUserDto>>.Ok(list);
    }

    public async Task EnsureProfileExistsAsync(Guid authUserId, string username, string email, string role)
    {
        var exists = await _db.UserProfiles.AnyAsync(p => p.AuthUserId == authUserId);
        if (exists) return;

        _db.UserProfiles.Add(new UserProfile
        {
            AuthUserId = authUserId,
            Username = username.ToLower().Trim(),
            FullName = username,
            Email = email.ToLower().Trim(),
            Role = role
        });

        await _db.SaveChangesAsync();
    }

    private ProfileResponse MapToResponse(UserProfile profile, bool isFollowed) =>
        new(
            profile.Id,
            profile.AuthUserId,
            profile.Username,
            profile.FullName,
            profile.Email,
            profile.Role,
            profile.Bio,
            _avatarStorage.ResolvePublicUrl(profile.AvatarPath),
            profile.DateOfBirth,
            profile.Phone,
            profile.Gender,
            profile.Address,
            profile.Education,
            profile.Occupation,
            JsonSerializer.Deserialize<List<string>>(profile.InterestsJson) ?? new(),
            profile.Followers.Count(f => f.Status == FollowStatus.Accepted),
            profile.Following.Count(f => f.Status == FollowStatus.Accepted),
            isFollowed,
            profile.CreatedAt
        );
}
