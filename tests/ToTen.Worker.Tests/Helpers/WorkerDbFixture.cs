using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using ToTen.Worker.Data;

namespace ToTen.Worker.Tests.Helpers;

public class WorkerDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    public WorkerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(_db.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__WorkerEFMigrationsHistory"))
            .Options;

        return new WorkerDbContext(options);
    }
}

[CollectionDefinition("WorkerDb")]
public class WorkerDbCollection : ICollectionFixture<WorkerDbFixture>;
