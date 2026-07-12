using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

// AuditLogEntries is owned and migrated by ToTen.Worker (WorkerDbContext) — Api only reads it.
// ExcludeFromMigrations keeps Api's own migrations from trying to create/alter this table.
public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries", t => t.ExcludeFromMigrations());

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Payload)
            .HasColumnType("jsonb");
    }
}
