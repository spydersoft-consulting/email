using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;

namespace Spydersoft.EmailApi.Infrastructure.Data;

public sealed class EmailDbContext(DbContextOptions<EmailDbContext> options) : DbContext(options)
{
    public DbSet<EmailMessageEntity> Messages => Set<EmailMessageEntity>();

    public DbSet<EmailEventEntity> Events => Set<EmailEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailMessageEntity>(e =>
        {
            e.ToTable("email_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(32);
            if (Database.IsNpgsql())
            {
                e.Property(m => m.To).HasColumnType("jsonb");
                e.Property(m => m.Cc).HasColumnType("jsonb");
                e.Property(m => m.Bcc).HasColumnType("jsonb");
                e.Property(m => m.Tags).HasColumnType("jsonb");
                e.Property(m => m.Attachments).HasColumnType("jsonb");
            }
            else
            {
                e.Property(m => m.Tags).HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                e.Property(m => m.Attachments).HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<AttachmentMeta>>(v, (JsonSerializerOptions?)null)!);
            }

            e.HasIndex(m => m.ResendId);
            e.HasIndex(m => new { m.SenderClientId, m.CreatedAt });
            e.HasIndex(m => m.Status);
            e.HasIndex(m => m.CreatedAt).IsDescending();
            e.HasIndex(m => new { m.IdempotencyKey, m.SenderClientId }).IsUnique();
            e.HasMany(m => m.Events).WithOne().HasForeignKey(ev => ev.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailEventEntity>(e =>
        {
            e.ToTable("email_events");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.RawPayload).HasColumnType("jsonb");
            e.HasIndex(ev => ev.WebhookId).IsUnique();
            e.HasIndex(ev => new { ev.MessageId, ev.OccurredAt });
        });
    }
}
