using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class ManifestConfiguration : IEntityTypeConfiguration<Manifest>
{
    public void Configure(EntityTypeBuilder<Manifest> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasOne(m => m.SourceLocation)
            .WithMany()
            .HasForeignKey(m => m.SourceLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.DestinationLocation)
            .WithMany()
            .HasForeignKey(m => m.DestinationLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Organization)
            .WithMany()
            .HasForeignKey(m => m.OrganizationId);

        builder.HasMany<Box>()
            .WithOne(b => b.Manifest)
            .HasForeignKey(b => b.ManifestId)
            .OnDelete(DeleteBehavior.SetNull);

        // ManifestStatus enum: Draft = 0, Pending = 1, InTransit = 2, Received = 3, Cancelled = 4
        builder.ToTable(t => t.HasCheckConstraint("CK_Manifests_Status_Range", "\"Status\" BETWEEN 0 AND 4"));
    }
}
