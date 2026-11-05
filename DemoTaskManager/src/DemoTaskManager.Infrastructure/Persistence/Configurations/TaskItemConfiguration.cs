using DemoTaskManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Infrastructure.Persistence.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    /// <summary>
    /// SQL Server and SQLite both drop <see cref="DateTimeKind"/>; this converter restores
    /// UTC on read so the whole stack stays unambiguously UTC.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(v => v.HasValue ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(TaskItem.TitleMaxLength);

        builder.Property(x => x.Description)
            .HasMaxLength(TaskItem.DescriptionMaxLength);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TaskStatus.Todo);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasConversion(UtcConverter);

        builder.Property(x => x.DueDateUtc)
            .HasConversion(NullableUtcConverter);

        builder.Property(x => x.UpdatedAtUtc)
            .HasConversion(NullableUtcConverter);

        builder.Property(x => x.IsReminderSent)
            .IsRequired()
            .HasDefaultValue(false);

        // Supports both the paged list ordering and the overdue-reminder sweep.
        builder.HasIndex(x => new { x.Status, x.DueDateUtc });
        builder.HasIndex(x => new { x.IsReminderSent, x.DueDateUtc });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
