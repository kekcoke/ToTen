using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ToTen.Worker.Data;

// Worker's Program.cs uses top-level statements over Host.CreateApplicationBuilder, which EF's
// generic-host convention-based discovery doesn't reliably find (unlike ASP.NET's
// WebApplicationBuilder). This factory lets `dotnet ef migrations add` build a WorkerDbContext
// without booting the full host (Rebus/Service Bus included).
public class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ToTenDB")
            ?? "Host=localhost;Database=ToTen;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<WorkerDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__WorkerEFMigrationsHistory"));

        return new WorkerDbContext(optionsBuilder.Options);
    }
}
