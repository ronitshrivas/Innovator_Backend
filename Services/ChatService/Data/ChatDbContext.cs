using ChatService.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatService.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> Participants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Conversation>(c =>
        {
            c.HasKey(x => x.Id);
            c.HasMany(x => x.Participants)
             .WithOne(x => x.Conversation)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
            c.HasMany(x => x.Messages)
             .WithOne(x => x.Conversation)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
            c.HasOne(x => x.LastMessage)
             .WithMany()
             .HasForeignKey(x => x.LastMessageId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ConversationParticipant>(p =>
        {
            p.HasKey(x => x.Id);
            p.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
        });

        builder.Entity<Message>(m =>
        {
            m.HasKey(x => x.Id);
            m.HasIndex(x => x.ConversationId);
            m.HasIndex(x => x.CreatedAt);
            m.Property(x => x.Content).HasMaxLength(5000);
            m.HasOne(x => x.ReplyTo)
             .WithMany()
             .HasForeignKey(x => x.ReplyToId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
