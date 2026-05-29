using Azure.Core;
using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ToTen.Api.Data;

public static class DataExtensions
{
    public static WebApplicationBuilder AddToTenNpgsql<TContext>(
        this WebApplicationBuilder builder,
        string connectionStringName,
        TokenCredential credential
    ) where TContext : DbContext
    {
        if (builder.Environment.IsProduction())
        {
            builder.AddAzureNpgsqlDbContext<TContext>(
                connectionStringName,
                settings => settings.Credential = credential,
                configureDbContextOptions: options =>
                    options.UseNpgsql(npgsql => npgsql.UseNetTopologySuite())
            );
        }
        else
        {
            builder.AddNpgsqlDbContext<TContext>(
                connectionStringName,
                configureDbContextOptions: options =>
                    options.UseNpgsql(npgsql => npgsql.UseNetTopologySuite()));
        }

        return builder;
    }

    public static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        ToTenContext dbContext = scope.ServiceProvider
                                          .GetRequiredService<ToTenContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static void SeedCategories(DbContext context)
    {
        context.Set<Category>().AddRange(
            new Category { Name = "General" },
            new Category { Name = "Urgent" },
            new Category { Name = "Archived" },
            new Category { Name = "Favorites" },
            new Category { Name = "Upcoming" }
        );
    }

    private static void SeedInventoryItems(DbContext context)
    {
        var generalCategory = context.Set<Category>().First(c => c.Name == "General");
        var upcomingCategory = context.Set<Category>().First(c => c.Name == "Upcoming");
        var favoritesCategory = context.Set<Category>().First(c => c.Name == "Favorites");

        context.Set<InventoryItem>().AddRange(
            new InventoryItem
            {
                Name = "Mechanical Keyboard",
                CategoryId = generalCategory.Id,
                Description = "RGB backlit mechanical keyboard with Cherry MX switches",
                LastUpdatedBy = "system",
                OwnerId = "demo"
            },
            new InventoryItem
            {
                Name = "4K Monitor",
                CategoryId = upcomingCategory.Id,
                Description = "27-inch 4K UHD display with HDR support and 144Hz refresh rate",
                LastUpdatedBy = "system",
                OwnerId = "demo"
            },
            new InventoryItem
            {
                Name = "Wireless Mouse",
                CategoryId = favoritesCategory.Id,
                Description = "Ergonomic wireless mouse with programmable buttons and long battery life",
                LastUpdatedBy = "system",
                OwnerId = "demo"
            }
        );
    }
}
