using ToTen.Api.Data.Configurations;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Data;

public class ToTenContext(DbContextOptions<ToTenContext> options)
    : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ItemEntityConfiguration).Assembly);
    }
}
