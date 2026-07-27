using Microsoft.EntityFrameworkCore;
using ResearchService.Data;
using ResearchService.DTOs;
using ResearchService.Entities;

namespace ResearchService.Services;

public interface IResearchPaperService
{
    Task<ResearchListResponse> GetPapersAsync(string? search, string? type, string? status, int page, int limit);
    Task<ResearchPaperDetailResponse?> GetByIdAsync(int id);
    Task<ResearchPaperDto> UploadAsync(UploadResearchRequest request, IFormFile paperFile, IFormFile? researcherFile);
    Task<PaymentInitiateResponse?> InitiatePaymentAsync(int paperId);
}

public class ResearchPaperService : IResearchPaperService
{
    private readonly ResearchDbContext _db;
    private readonly IFileStorage _files;
    private readonly IConfiguration _config;

    public ResearchPaperService(ResearchDbContext db, IFileStorage files, IConfiguration config)
    {
        _db = db;
        _files = files;
        _config = config;
    }

    public async Task<ResearchListResponse> GetPapersAsync(string? search, string? type, string? status, int page, int limit)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;

        var query = _db.ResearchPapers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.Type == type);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        var papers = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return new ResearchListResponse(papers.Select(MapPaper).ToList(), page, limit);
    }

    public async Task<ResearchPaperDetailResponse?> GetByIdAsync(int id)
    {
        var paper = await _db.ResearchPapers
            .Include(p => p.Researchers)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (paper is null)
            return null;

        var researchers = paper.Researchers
            .OrderBy(r => r.Id)
            .Select(MapResearcher)
            .ToList();

        return new ResearchPaperDetailResponse(MapPaper(paper), researchers);
    }

    public async Task<ResearchPaperDto> UploadAsync(UploadResearchRequest request, IFormFile paperFile, IFormFile? researcherFile)
    {
        var isPaid = request.Type == "paid";

        var fileUrl = await _files.SaveAsync(paperFile, "research");
        string? researcherPdfUrl = researcherFile is not null
            ? await _files.SaveAsync(researcherFile, "research")
            : null;

        var paper = new ResearchPaper
        {
            Email = request.Email,
            Title = request.Title,
            Description = request.Description,
            FileUrl = fileUrl,
            Type = request.Type,
            Price = isPaid ? request.Price ?? 0 : 0,
            Status = "active",
            PaymentStatus = isPaid ? "pending" : "free"
        };

        var names = (request.ResearcherNames ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in names)
        {
            paper.Researchers.Add(new Researcher
            {
                Name = name,
                ProfilePdfUrl = researcherPdfUrl
            });
        }

        _db.ResearchPapers.Add(paper);
        await _db.SaveChangesAsync();

        return MapPaper(paper);
    }

    public async Task<PaymentInitiateResponse?> InitiatePaymentAsync(int paperId)
    {
        var paper = await _db.ResearchPapers.FirstOrDefaultAsync(p => p.Id == paperId);
        if (paper is null)
            return null;

        var pidx = Guid.NewGuid().ToString("N");
        var baseUrl = _config["Khalti:BaseUrl"] ?? "https://khalti.com/pay";

        // No live gateway callback here: record the pidx and grant access so the
        // client can complete the flow. Replace with verified payment handling
        // once real Khalti credentials are available.
        paper.KhaltiPidx = pidx;
        paper.PaymentStatus = "completed";
        paper.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new PaymentInitiateResponse(
            Pidx: pidx,
            PaymentUrl: $"{baseUrl}?pidx={pidx}",
            PaperId: paper.Id,
            Amount: (double)paper.Price,
            Status: "initiated");
    }

    private static ResearchPaperDto MapPaper(ResearchPaper p) => new(
        Id: p.Id,
        Email: p.Email,
        Title: p.Title,
        Description: p.Description,
        FileUrl: p.FileUrl,
        Type: p.Type,
        Price: (double)p.Price,
        Status: p.Status,
        PaymentStatus: p.PaymentStatus,
        KhaltiPidx: p.KhaltiPidx,
        CreatedAt: Iso(p.CreatedAt),
        UpdatedAt: Iso(p.UpdatedAt));

    private static ResearcherDto MapResearcher(Researcher r) => new(
        Id: r.Id,
        PaperId: r.PaperId,
        Name: r.Name,
        ProfilePdfUrl: r.ProfilePdfUrl,
        CreatedAt: Iso(r.CreatedAt));

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
