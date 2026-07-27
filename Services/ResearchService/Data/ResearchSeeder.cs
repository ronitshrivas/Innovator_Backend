using Microsoft.EntityFrameworkCore;
using ResearchService.Entities;

namespace ResearchService.Data;

/// <summary>
/// Inserts a couple of sample papers on first run so the research list is not
/// empty. No-ops once papers already exist.
/// </summary>
public static class ResearchSeeder
{
    public static async Task SeedAsync(ResearchDbContext db)
    {
        if (await db.ResearchPapers.AnyAsync())
            return;

        db.ResearchPapers.AddRange(
            new ResearchPaper
            {
                Email = "author@innovator.app",
                Title = "Edge Computing for Low-Latency Mobile Apps",
                Description = "A survey of edge-computing patterns and their impact on mobile latency.",
                FileUrl = "https://api.meta-tronix.com/uploads/research/sample-edge-computing.pdf",
                Type = "free",
                Price = 0m,
                Status = "active",
                PaymentStatus = "free",
                Researchers = new List<Researcher>
                {
                    new() { Name = "Dr. Anita Rai" }
                }
            },
            new ResearchPaper
            {
                Email = "author@innovator.app",
                Title = "Scalable Microservices with ASP.NET Core",
                Description = "Design principles and benchmarks for scaling .NET microservices.",
                FileUrl = "https://api.meta-tronix.com/uploads/research/sample-microservices.pdf",
                Type = "paid",
                Price = 499m,
                Status = "active",
                PaymentStatus = "pending",
                Researchers = new List<Researcher>
                {
                    new() { Name = "Ronit Shrivastav" },
                    new() { Name = "Dr. Suman Thapa" }
                }
            });

        await db.SaveChangesAsync();
    }
}
