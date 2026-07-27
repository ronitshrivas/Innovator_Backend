using Microsoft.EntityFrameworkCore;
using ResearchService.Entities;

namespace ResearchService.Data;

public class ResearchDbContext : DbContext
{
    public ResearchDbContext(DbContextOptions<ResearchDbContext> options) : base(options) { }

    public DbSet<ResearchPaper> ResearchPapers => Set<ResearchPaper>();
    public DbSet<Researcher> Researchers => Set<Researcher>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ResearchPaper>(entity =>
        {
            entity.Property(p => p.Price).HasColumnType("numeric(10,2)");
        });

        modelBuilder.Entity<Researcher>(entity =>
        {
            entity.HasOne(r => r.Paper)
                .WithMany(p => p.Researchers)
                .HasForeignKey(r => r.PaperId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
