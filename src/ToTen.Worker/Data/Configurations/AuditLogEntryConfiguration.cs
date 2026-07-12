using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Worker.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Payload)
            .HasColumnType("jsonb");

        builder.HasIndex(a => a.ItemId);
        builder.HasIndex(a => a.ManifestId);
        builder.HasIndex(a => a.EventType);
        builder.HasIndex(a => a.OccurredAt);
    }
}
