using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ToTen.Worker.Data;

public static class WorkerDataExtensions
{
    // Worker's own migrations history table name — kept distinct from ToTenContext's default
    // __EFMigrationsHistory so the two independently-migrated DbContexts (Api's ToTenContext,
    // Worker's WorkerDbContext) sharing the same physical "ToTen" database don't collide.
    private const string MigrationsHistoryTable = "__WorkerEFMigrationsHistory";

    public static IHostApplicationBuilder AddWorkerNpgsql<TContext>(
        this IHostApplicationBuilder builder,
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
                    options.UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            );
        }
        else
        {
            builder.AddNpgsqlDbContext<TContext>(
                connectionStringName,
                configureDbContextOptions: options =>
                    options.UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable)));
        }

        return builder;
    }

    public static async Task MigrateWorkerDbAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
