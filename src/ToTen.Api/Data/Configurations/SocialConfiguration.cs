using ToTen.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ToTen.Api.Data.Configurations;

public class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasMany(t => t.Messages)
            .WithOne(m => m.Thread)
            .HasForeignKey(m => m.ThreadId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasIndex(n => n.UserId);
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(np => np.Id);
        builder.HasIndex(np => np.UserId).IsUnique();
    }
}
