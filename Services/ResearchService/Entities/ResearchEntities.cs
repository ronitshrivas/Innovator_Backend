namespace ResearchService.Entities;

public class ResearchPaper
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>"free" or "paid".</summary>
    public string Type { get; set; } = "free";
    public decimal Price { get; set; }

    /// <summary>"active" or "pending".</summary>
    public string Status { get; set; } = "active";

    /// <summary>"free", "pending" or "completed".</summary>
    public string PaymentStatus { get; set; } = "free";
    public string? KhaltiPidx { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Researcher> Researchers { get; set; } = new();
}

public class Researcher
{
    public int Id { get; set; }
    public int PaperId { get; set; }
    public ResearchPaper Paper { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? ProfilePdfUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
