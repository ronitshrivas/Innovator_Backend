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
    Task<ApiResponse<CoverImageResponse>> UpdateCoverAsync(Guid authUserId, IFormFile file);
    Task<ApiResponse<bool>> DeleteCoverAsync(Guid authUserId);
    Task<ApiResponse<FollowActionResponse>> ToggleFollowAsync(Guid followerId, Guid targetAuthUserId);
    Task<ApiResponse<List<UserSummaryDto>>> GetFollowRequestsAsync(Guid authUserId);
    Task<ApiResponse<bool>> RespondToFollowRequestAsync(Guid ownerAuthId, Guid requesterAuthUserId, bool accept);
    Task<ApiResponse<List<UserSummaryDto>>> GetFollowersAsync(Guid authUserId, Guid requesterId);
    Task<ApiResponse<List<UserSummaryDto>>> GetFollowingAsync(Guid authUserId, Guid requesterId);
    Task<ApiResponse<BlockActionResponse>> ToggleBlockAsync(Guid blockerId, Guid targetAuthUserId);
    Task<ApiResponse<List<BlockedUserDto>>> GetBlockedListAsync(Guid authUserId);
    Task EnsureProfileExistsAsync(Guid authUserId, string username, string email, string role);
    Task<List<string>> GetBlockPairIdsAsync(Guid authUserId);
    Task<FollowGraph> GetFollowGraphAsync(Guid authUserId, int secondDegreeCap = 200);
    Task<ApiResponse<List<SuggestedUserDto>>> GetSuggestedUsersAsync(Guid authUserId, int limit);
    Task<ApiResponse<FindFriendsPageDto>> FindFriendsAsync(
        Guid authUserId, string? query, int page, int pageSize);
    Task<ApiResponse<bool>> DismissSuggestionAsync(Guid authUserId, Guid dismissedAuthUserId);
    Task<Dictionary<string, string?>> GetAvatarsAsync(IEnumerable<Guid> authUserIds);
    Task<Dictionary<string, AuthorInfo>> GetAuthorInfoAsync(IEnumerable<Guid> authUserIds, Guid? requesterId);
}

/// Compact per-author info the feed embeds: current avatar, occupation and
/// whether the requesting user follows them.
public record AuthorInfo(string? Avatar, string? Occupation, bool IsFollowed, string? Username = null);

public record FollowGraph(List<string> Following, List<string> SecondDegree);

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

        var isFollowed = await IsFollowedByAsync(profile, requesterId);
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

        var isFollowed = await IsFollowedByAsync(profile, requesterId);
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
        if (request.Educations is not null)
            profile.EducationsJson = JsonSerializer.Serialize(request.Educations);
        if (request.Occupations is not null)
            profile.OccupationsJson = JsonSerializer.Serialize(request.Occupations);
        if (request.Links is not null)
            profile.LinksJson = JsonSerializer.Serialize(request.Links);

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

    public async Task<ApiResponse<CoverImageResponse>> UpdateCoverAsync(Guid authUserId, IFormFile file)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (profile == null)
            return ApiResponse<CoverImageResponse>.Fail("Profile not found.");

        _avatarStorage.DeleteAvatar(profile.CoverImagePath);

        var relativePath = await _avatarStorage.SaveCoverAsync(file, profile.Username);
        profile.CoverImagePath = relativePath;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var publicUrl = _avatarStorage.ResolvePublicUrl(relativePath);
        return ApiResponse<CoverImageResponse>.Ok(
            new CoverImageResponse(publicUrl), "Cover image updated.");
    }

    public async Task<ApiResponse<bool>> DeleteCoverAsync(Guid authUserId)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (profile == null)
            return ApiResponse<bool>.Fail("Profile not found.");

        _avatarStorage.DeleteAvatar(profile.CoverImagePath);
        profile.CoverImagePath = null;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Cover image removed.");
    }

    // requesterId is an AUTH user id; follows store PROFILE ids. Resolve the
    // requester's profile id first, then check if they follow this profile.
    private async Task<bool> IsFollowedByAsync(UserProfile target, Guid? requesterAuthId)
    {
        if (!requesterAuthId.HasValue) return false;

        var requesterProfileId = await _db.UserProfiles
            .Where(p => p.AuthUserId == requesterAuthId.Value)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

        if (requesterProfileId is null) return false;

        return target.Followers.Any(f =>
            f.FollowerId == requesterProfileId.Value && f.Status == FollowStatus.Accepted);
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
            // Toggling off cancels a pending request or unfollows an accepted one.
            _db.Follows.Remove(existing);
            await _db.SaveChangesAsync();
            var msg = existing.Status == FollowStatus.Pending
                ? "Follow request cancelled."
                : "Unfollowed.";
            return ApiResponse<FollowActionResponse>.Ok(new FollowActionResponse(false, msg));
        }

        // Private target → create a pending request instead of an instant follow.
        var targetPrivate = await _db.UserSettings
            .Where(s => s.UserId == targetAuthUserId)
            .Select(s => (bool?)s.PrivateAccount)
            .FirstOrDefaultAsync() ?? false;

        var status = targetPrivate ? FollowStatus.Pending : FollowStatus.Accepted;

        _db.Follows.Add(new Follow
        {
            FollowerId = followerProfile.Id,
            FollowingId = targetProfile.Id,
            Status = status
        });

        await _db.SaveChangesAsync();

        return targetPrivate
            ? ApiResponse<FollowActionResponse>.Ok(
                new FollowActionResponse(false, "Follow request sent.", "pending"))
            : ApiResponse<FollowActionResponse>.Ok(
                new FollowActionResponse(true, "Following.", "accepted"));
    }

    public async Task<ApiResponse<List<UserSummaryDto>>> GetFollowRequestsAsync(Guid authUserId)
    {
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (profile == null)
            return ApiResponse<List<UserSummaryDto>>.Fail("User not found.");

        var requests = await _db.Follows
            .Where(f => f.FollowingId == profile.Id && f.Status == FollowStatus.Pending)
            .Include(f => f.Follower)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var summaries = requests.Select(f => new UserSummaryDto(
            f.Follower.AuthUserId,
            f.Follower.Username,
            f.Follower.FullName,
            _avatarStorage.ResolvePublicUrl(f.Follower.AvatarPath),
            f.Follower.Role,
            f.Follower.Occupation,
            false)).ToList();

        return ApiResponse<List<UserSummaryDto>>.Ok(summaries);
    }

    public async Task<ApiResponse<bool>> RespondToFollowRequestAsync(
        Guid ownerAuthId, Guid requesterAuthUserId, bool accept)
    {
        var ownerProfile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == ownerAuthId);
        var requesterProfile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == requesterAuthUserId);

        if (ownerProfile == null || requesterProfile == null)
            return ApiResponse<bool>.Fail("User not found.");

        var request = await _db.Follows
            .FirstOrDefaultAsync(f => f.FollowingId == ownerProfile.Id &&
                                      f.FollowerId == requesterProfile.Id &&
                                      f.Status == FollowStatus.Pending);

        if (request == null)
            return ApiResponse<bool>.Fail("No pending request from that user.");

        if (accept)
        {
            request.Status = FollowStatus.Accepted;
            request.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Follows.Remove(request);
        }

        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, accept ? "Request accepted." : "Request rejected.");
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
                f.Follower.Occupation,
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
                f.FollowingUser.Occupation,
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

    // All auth_user_ids the given user has blocked OR been blocked by, so other
    // services (e.g. search) can hide those users in both directions.
    public async Task<List<string>> GetBlockPairIdsAsync(Guid authUserId)
    {
        var me = await _db.UserProfiles
            .Where(p => p.AuthUserId == authUserId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
        if (me is null) return new();

        var iBlocked = await _db.BlockedUsers
            .Where(b => b.BlockerId == me.Value)
            .Select(b => b.Blocked.AuthUserId.ToString())
            .ToListAsync();

        var blockedMe = await _db.BlockedUsers
            .Where(b => b.BlockedId == me.Value)
            .Select(b => b.Blocker.AuthUserId.ToString())
            .ToListAsync();

        return iBlocked.Concat(blockedMe).Distinct().ToList();
    }

    // The user's accepted follows (1st degree) and the people those follows
    // follow (2nd degree), for feed candidate generation. Auth user ids.
    public async Task<FollowGraph> GetFollowGraphAsync(Guid authUserId, int secondDegreeCap = 200)
    {
        var me = await _db.UserProfiles
            .Where(p => p.AuthUserId == authUserId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();
        if (me is null) return new FollowGraph(new(), new());

        // 1st degree: profile ids I follow (accepted).
        var followingProfileIds = await _db.Follows
            .Where(f => f.FollowerId == me.Value && f.Status == FollowStatus.Accepted)
            .Select(f => f.FollowingId)
            .ToListAsync();

        var following = await _db.UserProfiles
            .Where(p => followingProfileIds.Contains(p.Id))
            .Select(p => p.AuthUserId.ToString())
            .ToListAsync();

        // 2nd degree: accepted follows of my follows, minus me and my follows,
        // ranked by how many of my follows follow them.
        var secondDegree = new List<string>();
        if (followingProfileIds.Count > 0)
        {
            var excluded = followingProfileIds.ToHashSet();
            excluded.Add(me.Value);

            var ranked = await _db.Follows
                .Where(f => followingProfileIds.Contains(f.FollowerId) &&
                            f.Status == FollowStatus.Accepted &&
                            !excluded.Contains(f.FollowingId))
                .GroupBy(f => f.FollowingId)
                .Select(g => new { ProfileId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(secondDegreeCap)
                .ToListAsync();

            var rankedIds = ranked.Select(x => x.ProfileId).ToList();
            var idToAuth = await _db.UserProfiles
                .Where(p => rankedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.AuthUserId })
                .ToListAsync();
            var map = idToAuth.ToDictionary(x => x.Id, x => x.AuthUserId.ToString());

            secondDegree = rankedIds
                .Where(map.ContainsKey)
                .Select(id => map[id])
                .ToList();
        }

        return new FollowGraph(following, secondDegree);
    }

    // "Suggested for you": people the viewer should follow, ranked by mutual
    // connections, category overlap, and popularity. Excludes already-followed,
    // blocked (either direction), self, and recently-dismissed suggestions.
    public async Task<ApiResponse<List<SuggestedUserDto>>> GetSuggestedUsersAsync(
        Guid authUserId, int limit)
    {
        limit = Math.Clamp(limit, 1, 50);

        var me = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (me is null)
            return ApiResponse<List<SuggestedUserDto>>.Ok(new());

        // People I already follow (profile ids) — the seed for 2nd-degree.
        var myFollowingIds = await _db.Follows
            .Where(f => f.FollowerId == me.Id && f.Status == FollowStatus.Accepted)
            .Select(f => f.FollowingId)
            .ToListAsync();

        // Exclusions: self, my follows, blocked pairs, recent dismissals.
        var excluded = myFollowingIds.ToHashSet();
        excluded.Add(me.Id);

        var blockedAuthIds = await GetBlockPairIdsAsync(authUserId);
        if (blockedAuthIds.Count > 0)
        {
            var blockedProfileIds = await _db.UserProfiles
                .Where(p => blockedAuthIds.Contains(p.AuthUserId.ToString()))
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var id in blockedProfileIds) excluded.Add(id);
        }

        var dismissedSince = DateTime.UtcNow.AddDays(-30);
        var dismissedAuthIds = await _db.SuggestionDismissals
            .Where(d => d.UserId == authUserId && d.DismissedAt > dismissedSince)
            .Select(d => d.DismissedUserId)
            .ToListAsync();
        if (dismissedAuthIds.Count > 0)
        {
            var dismissedProfileIds = await _db.UserProfiles
                .Where(p => dismissedAuthIds.Contains(p.AuthUserId))
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var id in dismissedProfileIds) excluded.Add(id);
        }

        // ---- Candidate generation ----
        // (a) 2nd-degree: people my follows follow, with mutual-follow counts.
        var mutualCounts = new Dictionary<Guid, int>();
        if (myFollowingIds.Count > 0)
        {
            var rows = await _db.Follows
                .Where(f => myFollowingIds.Contains(f.FollowerId) &&
                            f.Status == FollowStatus.Accepted &&
                            !excluded.Contains(f.FollowingId))
                .GroupBy(f => f.FollowingId)
                .Select(g => new { ProfileId = g.Key, Count = g.Count() })
                .ToListAsync();
            foreach (var r in rows) mutualCounts[r.ProfileId] = r.Count;
        }

        // (b) popular/rising users I don't follow (dampened), to backfill.
        var popular = await _db.UserProfiles
            .Where(p => p.IsActive && !excluded.Contains(p.Id))
            .Select(p => new { p.Id, Followers = p.Followers.Count(f => f.Status == FollowStatus.Accepted) })
            .OrderByDescending(x => x.Followers)
            .Take(limit * 4)
            .ToListAsync();

        var candidateIds = mutualCounts.Keys
            .Concat(popular.Select(p => p.Id))
            .Distinct()
            .ToList();
        if (candidateIds.Count == 0)
            return ApiResponse<List<SuggestedUserDto>>.Ok(new());

        var candidates = await _db.UserProfiles
            .Where(p => candidateIds.Contains(p.Id) && p.IsActive)
            .Select(p => new
            {
                p.Id, p.AuthUserId, p.Username, p.FullName, p.AvatarPath,
                p.Occupation, p.InterestsJson,
                Followers = p.Followers.Count(f => f.Status == FollowStatus.Accepted)
            })
            .ToListAsync();

        var myInterests = SafeInterests(me.InterestsJson);
        var rng = new Random(authUserId.GetHashCode());

        // ---- Ranking ----
        var scored = candidates.Select(c =>
        {
            var mutual = mutualCounts.TryGetValue(c.Id, out var m) ? m : 0;
            var theirInterests = SafeInterests(c.InterestsJson);
            var overlap = myInterests.Intersect(theirInterests, StringComparer.OrdinalIgnoreCase).ToList();

            var score =
                  4.0 * mutual                               // mutual connections — highest
                + 1.5 * overlap.Count                        // category overlap
                + 0.6 * Math.Log10(c.Followers + 1)          // popularity, dampened
                + 0.3 * rng.NextDouble();                    // exploration jitter

            var reason = mutual > 0
                ? $"Followed by {mutual} {(mutual == 1 ? "person" : "people")} you follow"
                : overlap.Count > 0
                    ? $"Active in {overlap[0]}"
                    : "Popular on Innovator";

            return new { c, score, mutual, reason };
        })
        .OrderByDescending(x => x.score)
        .Take(limit)
        .ToList();

        var result = scored.Select(x => new SuggestedUserDto(
            x.c.AuthUserId,
            x.c.Username,
            x.c.FullName,
            _avatarStorage.ResolvePublicUrl(x.c.AvatarPath),
            x.c.Occupation,
            x.mutual,
            x.reason)).ToList();

        return ApiResponse<List<SuggestedUserDto>>.Ok(result);
    }

    public async Task<ApiResponse<FindFriendsPageDto>> FindFriendsAsync(
        Guid authUserId, string? query, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var me = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.AuthUserId == authUserId);
        if (me is null)
        {
            return ApiResponse<FindFriendsPageDto>.Ok(
                new FindFriendsPageDto(Array.Empty<FindFriendDto>(), page, pageSize, false));
        }

        // Exclude self and either side of a block relationship.
        var excludedProfileIds = new HashSet<Guid> { me.Id };
        var blockedAuthIds = await GetBlockPairIdsAsync(authUserId);
        if (blockedAuthIds.Count > 0)
        {
            var blockedProfileIds = await _db.UserProfiles
                .Where(p => blockedAuthIds.Contains(p.AuthUserId.ToString()))
                .Select(p => p.Id)
                .ToListAsync();
            foreach (var id in blockedProfileIds) excludedProfileIds.Add(id);
        }

        // Base query: active users I'm allowed to see, optionally name/username
        // filtered. Case-insensitive contains on either field.
        var baseQuery = _db.UserProfiles
            .Where(p => p.IsActive && !excludedProfileIds.Contains(p.Id));

        var trimmed = query?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            var q = trimmed.ToLower();
            baseQuery = baseQuery.Where(p =>
                p.Username.ToLower().Contains(q) ||
                p.FullName.ToLower().Contains(q));
        }

        // Order by popularity (accepted followers), then username for stability.
        var ordered = baseQuery
            .Select(p => new
            {
                Profile = p,
                Followers = p.Followers.Count(f => f.Status == FollowStatus.Accepted)
            })
            .OrderByDescending(x => x.Followers)
            .ThenBy(x => x.Profile.Username);

        // Fetch one extra row to know if there's a next page.
        var skip = (page - 1) * pageSize;
        var rows = await ordered
            .Skip(skip)
            .Take(pageSize + 1)
            .Select(x => x.Profile)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        if (hasMore) rows = rows.Take(pageSize).ToList();

        // Which of these the viewer already follows (and the follow status).
        var candidateIds = rows.Select(p => p.Id).ToList();
        var myFollows = await _db.Follows
            .Where(f => f.FollowerId == me.Id && candidateIds.Contains(f.FollowingId))
            .Select(f => new { f.FollowingId, f.Status })
            .ToListAsync();
        var statusByProfile = myFollows.ToDictionary(f => f.FollowingId, f => f.Status);

        var people = rows.Select(p =>
        {
            // Headline: occupation, else education (LinkedIn-style fallback).
            var headline = !string.IsNullOrWhiteSpace(p.Occupation)
                ? p.Occupation
                : (!string.IsNullOrWhiteSpace(p.Education) ? p.Education : null);

            var followStatus = "none";
            var isFollowed = false;
            if (statusByProfile.TryGetValue(p.Id, out var status))
            {
                followStatus = status switch
                {
                    FollowStatus.Accepted => "accepted",
                    FollowStatus.Pending => "pending",
                    _ => "none"
                };
                isFollowed = status == FollowStatus.Accepted;
            }

            return new FindFriendDto(
                p.AuthUserId,
                p.Username,
                p.FullName,
                _avatarStorage.ResolvePublicUrl(p.AvatarPath),
                headline,
                isFollowed,
                followStatus);
        }).ToList();

        return ApiResponse<FindFriendsPageDto>.Ok(
            new FindFriendsPageDto(people, page, pageSize, hasMore));
    }

    public async Task<ApiResponse<bool>> DismissSuggestionAsync(
        Guid authUserId, Guid dismissedAuthUserId)
    {
        var existing = await _db.SuggestionDismissals
            .FirstOrDefaultAsync(d => d.UserId == authUserId &&
                                      d.DismissedUserId == dismissedAuthUserId);
        if (existing != null)
        {
            existing.DismissedAt = DateTime.UtcNow; // refresh the 30-day window
        }
        else
        {
            _db.SuggestionDismissals.Add(new SuggestionDismissal
            {
                UserId = authUserId,
                DismissedUserId = dismissedAuthUserId
            });
        }
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Ok(true, "Suggestion dismissed.");
    }

    private static List<string> SafeInterests(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    // Returns a map of auth_user_id -> resolved avatar URL for the given users,
    // so other services (e.g. feed) can show each author's current avatar.
    public async Task<Dictionary<string, string?>> GetAvatarsAsync(IEnumerable<Guid> authUserIds)
    {
        var ids = authUserIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _db.UserProfiles
            .Where(p => ids.Contains(p.AuthUserId))
            .Select(p => new { p.AuthUserId, p.AvatarPath })
            .ToListAsync();

        return rows.ToDictionary(
            r => r.AuthUserId.ToString(),
            r => (string?)_avatarStorage.ResolvePublicUrl(r.AvatarPath));
    }

    // Batch author info for the feed: avatar + occupation + is_followed.
    public async Task<Dictionary<string, AuthorInfo>> GetAuthorInfoAsync(
        IEnumerable<Guid> authUserIds, Guid? requesterId)
    {
        var ids = authUserIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await _db.UserProfiles
            .Where(p => ids.Contains(p.AuthUserId))
            .Select(p => new { p.Id, p.AuthUserId, p.AvatarPath, p.Occupation, p.Username })
            .ToListAsync();

        // Which of these profiles does the requester follow?
        var followedProfileIds = new HashSet<Guid>();
        if (requesterId.HasValue)
        {
            var requesterProfileId = await _db.UserProfiles
                .Where(p => p.AuthUserId == requesterId.Value)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            if (requesterProfileId is not null)
            {
                var targetProfileIds = rows.Select(r => r.Id).ToList();
                followedProfileIds = (await _db.Follows
                    .Where(f => f.FollowerId == requesterProfileId.Value &&
                                targetProfileIds.Contains(f.FollowingId) &&
                                f.Status == FollowStatus.Accepted)
                    .Select(f => f.FollowingId)
                    .ToListAsync()).ToHashSet();
            }
        }

        return rows.ToDictionary(
            r => r.AuthUserId.ToString(),
            r => new AuthorInfo(
                _avatarStorage.ResolvePublicUrl(r.AvatarPath),
                r.Occupation,
                followedProfileIds.Contains(r.Id),
                r.Username));
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
            string.IsNullOrEmpty(profile.CoverImagePath)
                ? null
                : _avatarStorage.ResolvePublicUrl(profile.CoverImagePath),
            profile.DateOfBirth,
            profile.Phone,
            profile.Gender,
            profile.Address,
            profile.Education,
            profile.Occupation,
            JsonSerializer.Deserialize<List<string>>(profile.InterestsJson) ?? new(),
            JsonSerializer.Deserialize<List<string>>(profile.EducationsJson) ?? new(),
            JsonSerializer.Deserialize<List<string>>(profile.OccupationsJson) ?? new(),
            JsonSerializer.Deserialize<List<ProfileLink>>(profile.LinksJson) ?? new(),
            profile.Followers.Count(f => f.Status == FollowStatus.Accepted),
            profile.Following.Count(f => f.Status == FollowStatus.Accepted),
            isFollowed,
            profile.CreatedAt
        );
}
