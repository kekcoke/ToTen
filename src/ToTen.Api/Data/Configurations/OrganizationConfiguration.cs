using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).IsRequired().HasMaxLength(255);
        builder.Property(o => o.Type).IsRequired().HasMaxLength(50);

        builder.ToTable(t => t.HasCheckConstraint("CK_Organizations_Type", "\"Type\" IN ('Household', 'Business')"));
    }
}

public class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.HasKey(m => new { m.OrganizationId, m.UserId });

        builder.HasOne(m => m.Organization)
            .WithMany(o => o.Memberships)
            .HasForeignKey(m => m.OrganizationId);
    }
}
