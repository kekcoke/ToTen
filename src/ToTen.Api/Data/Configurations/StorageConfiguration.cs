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

        builder.Property(l => l.Coordinates)
            .HasColumnType("geometry(Point, 4326)");

        // Indices
        builder.HasIndex(l => l.Coordinates)
            .HasMethod("gist");

        builder.HasIndex(l => l.Metadata)
            .HasMethod("gin");

        builder.HasIndex(l => l.OwnerId);
        builder.HasIndex(l => l.OrganizationId);

        builder.HasOne(l => l.Organization)
            .WithMany(o => o.Locations)
            .HasForeignKey(l => l.OrganizationId);
    }
}

public class BoxConfiguration : IEntityTypeConfiguration<Box>
{
    public void Configure(EntityTypeBuilder<Box> builder)
    {
        builder.HasKey(b => b.Id);
        
        builder.HasOne(b => b.Location)
            .WithMany()
            .HasForeignKey(b => b.LocationId);

        builder.HasOne(b => b.Organization)
            .WithMany(o => o.Boxes)
            .HasForeignKey(b => b.OrganizationId);
            
        builder.HasIndex(b => b.OwnerId);
        builder.HasIndex(b => b.OrganizationId);
    }
}
