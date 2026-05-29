using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(l => l.Id);
        
        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(l => l.Metadata)
            .HasColumnType("jsonb");

        // PostGIS Index
        builder.HasIndex(l => l.Coordinates)
            .HasMethod("gist");

        builder.HasIndex(l => l.OwnerId);
        builder.HasIndex(l => l.OrganizationId);
    }
}
