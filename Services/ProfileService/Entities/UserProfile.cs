using Innovator.Shared.Entities;

namespace ProfileService.Entities;

public class UserProfile : BaseEntity
{
    public Guid AuthUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "innovator";
    public string? Bio { get; set; }
    public string? AvatarPath { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? Education { get; set; }
    public string? Occupation { get; set; }
    public string InterestsJson { get; set; } = "[]";

    // Multi-value professional detail, stored as JSON arrays so the app can
    // show several entries (like LinkedIn). Single Education/Occupation above
    // are kept for backward compatibility.
    public string EducationsJson { get; set; } = "[]";   // ["MSc CS, ...", ...]
    public string OccupationsJson { get; set; } = "[]";  // ["Engineer @X", ...]
    public string LinksJson { get; set; } = "[]";        // [{"label":"LinkedIn","url":"..."}]

    public bool IsActive { get; set; } = true;

    public List<Follow> Followers { get; set; } = new();
    public List<Follow> Following { get; set; } = new();
    public List<BlockedUser> BlockedUsers { get; set; } = new();
}

public class Follow : BaseEntity
{
    public Guid FollowerId { get; set; }
    public UserProfile Follower { get; set; } = null!;

    public Guid FollowingId { get; set; }
    public UserProfile FollowingUser { get; set; } = null!;

    public FollowStatus Status { get; set; } = FollowStatus.Accepted;
}

public enum FollowStatus
{
    Pending,
    Accepted,
    Rejected
}

public class BlockedUser : BaseEntity
{
    public Guid BlockerId { get; set; }
    public UserProfile Blocker { get; set; } = null!;

    public Guid BlockedId { get; set; }
    public UserProfile Blocked { get; set; } = null!;
}
