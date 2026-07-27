using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ResearchService.DTOs;

public record ResearchPaperDto(
    int Id,
    string Email,
    string Title,
    string? Description,
    string FileUrl,
    string Type,
    double Price,
    string Status,
    string PaymentStatus,
    string? KhaltiPidx,
    string CreatedAt,
    string UpdatedAt
);

public record ResearchListResponse(
    List<ResearchPaperDto> Data,
    int Page,
    int Limit
);

public record ResearcherDto(
    int Id,
    int PaperId,
    string Name,
    string? ProfilePdfUrl,
    string CreatedAt
);

public record ResearchPaperDetailResponse(
    ResearchPaperDto Paper,
    List<ResearcherDto> Researchers
);

public record UploadResearchRequest(
    [Required] string Email,
    [Required] string Title,
    string? Description,
    [Required] string Type,
    int? Price,
    string? ResearcherNames
);

/// <summary>
/// Multipart upload payload. Field names are bound explicitly so the snake_case
/// keys the Flutter app sends (paper_file, researcher_names, …) map correctly.
/// </summary>
public class UploadResearchForm
{
    [Required]
    [FromForm(Name = "email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [FromForm(Name = "title")]
    public string Title { get; set; } = string.Empty;

    [FromForm(Name = "description")]
    public string? Description { get; set; }

    [Required]
    [FromForm(Name = "type")]
    public string Type { get; set; } = "free";

    [FromForm(Name = "price")]
    public int? Price { get; set; }

    [FromForm(Name = "researcher_names")]
    public string? ResearcherNames { get; set; }

    [Required]
    [FromForm(Name = "paper_file")]
    public IFormFile PaperFile { get; set; } = null!;

    [FromForm(Name = "researcher_files")]
    public IFormFile? ResearcherFile { get; set; }
}

public record PaymentInitiateResponse(
    string Pidx,
    string PaymentUrl,
    int PaperId,
    double Amount,
    string Status
);
