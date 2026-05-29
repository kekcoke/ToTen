using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class ItemLineageConfiguration : IEntityTypeConfiguration<ItemLineage>
{
    public void Configure(EntityTypeBuilder<ItemLineage> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.StateSnapshot)
            .HasColumnType("jsonb");

        builder.HasOne(l => l.InventoryItem)
            .WithMany()
            .HasForeignKey(l => l.InventoryItemId);

        builder.HasOne(l => l.Transaction)
            .WithMany()
            .HasForeignKey(l => l.TransactionId)
            .IsRequired(false);

        builder.HasIndex(l => l.InventoryItemId);
        builder.HasIndex(l => l.ChangedByUserId);
    }
}
