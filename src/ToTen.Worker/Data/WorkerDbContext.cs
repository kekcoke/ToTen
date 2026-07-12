using Microsoft.EntityFrameworkCore;
using ToTen.Worker.Data.Configurations;

namespace ToTen.Worker.Data;

public class WorkerDbContext(DbContextOptions<WorkerDbContext> options)
    : DbContext(options)
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLogEntryConfiguration).Assembly);
    }
}
