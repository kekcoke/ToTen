using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Price).HasPrecision(18, 2);
        
        builder.HasOne(l => l.InventoryItem)
            .WithMany()
            .HasForeignKey(l => l.InventoryItemId);
    }
}

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Amount).HasPrecision(18, 2);
        builder.Property(o => o.CounterAmount).HasPrecision(18, 2);

        builder.HasOne(o => o.Listing)
            .WithMany()
            .HasForeignKey(o => o.ListingId);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Amount).HasPrecision(18, 2);

        builder.HasOne(t => t.InventoryItem)
            .WithMany()
            .HasForeignKey(t => t.InventoryItemId);
    }
}
