using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
            e.Property(m => m.Tags).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null),
                new ValueComparer<Dictionary<string, string>?>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                    v => v == null ? null : new Dictionary<string, string>(v)));
            e.Property(m => m.Attachments).HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AttachmentMeta>>(v, (JsonSerializerOptions?)null)!,
                new ValueComparer<List<AttachmentMeta>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)),
                    v => v.ToList()));

            if (Database.IsNpgsql())
            {
                e.Property(m => m.To).HasColumnType("jsonb");
                e.Property(m => m.Cc).HasColumnType("jsonb");
                e.Property(m => m.Bcc).HasColumnType("jsonb");
                e.Property(m => m.Tags).HasColumnType("jsonb");
                e.Property(m => m.Attachments).HasColumnType("jsonb");
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
