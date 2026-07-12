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

        builder.ToTable(t => t.HasCheckConstraint("CK_Listings_Price_Positive", "\"Price\" > 0"));
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

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Offers_Amount_Positive", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_Offers_CounterAmount_Positive", "\"CounterAmount\" IS NULL OR \"CounterAmount\" > 0");
            // OfferStatus enum: Pending = 0, Accepted = 1, Rejected = 2, Countered = 3
            t.HasCheckConstraint("CK_Offers_Status_Range", "\"Status\" BETWEEN 0 AND 3");
        });
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

        builder.ToTable(t => t.HasCheckConstraint("CK_Transactions_Amount_Positive", "\"Amount\" > 0"));
    }
}
