using ToTen.Api.Data.Configurations;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Data;

public class ToTenContext(DbContextOptions<ToTenContext> options)
    : DbContext(options)
{
    // Core & Storage
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Box> Boxes => Set<Box>();

    // Manifest & Marketplace
    public DbSet<Manifest> Manifests => Set<Manifest>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // Social & Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // Lineage
    public DbSet<ItemLineage> ItemLineages => Set<ItemLineage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ItemEntityConfiguration).Assembly);

        // Many-to-many relationship for OrganizationMembership
        modelBuilder.Entity<OrganizationMembership>()
            .HasKey(m => new { m.OrganizationId, m.UserId });
    }
}
