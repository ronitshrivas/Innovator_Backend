using Innovator.Shared.Entities;

namespace ProfileService.Entities;

/// Per-user preferences (1:1 with a user, keyed by AuthUserId). A default row is
/// created on first access so reads never 404.
public class UserSettings : BaseEntity
{
    public Guid UserId { get; set; }

    // Notifications
    public bool PushEnabled { get; set; } = true;
    public bool NotifyLikes { get; set; } = true;
    public bool NotifyComments { get; set; } = true;
    public bool NotifyFollows { get; set; } = true;
    public bool NotifyMentions { get; set; } = true;
    public bool NotifyMessages { get; set; } = true;
    public bool NotifyReposts { get; set; } = true;
    public bool EmailDigest { get; set; } = false;

    // Privacy
    public bool PrivateAccount { get; set; } = false;
    public string WhoCanMessage { get; set; } = "everyone"; // everyone | followers | none
    public string WhoCanComment { get; set; } = "everyone"; // everyone | followers | none
    public bool ShowActivityStatus { get; set; } = true;
    public bool ShowInSearch { get; set; } = true;

    // App
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "system"; // system | light | dark
    public string? Timezone { get; set; }
}
