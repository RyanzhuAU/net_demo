using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Domain.Entities;

/// <summary>
/// Aggregate root representing a single unit of work owned by a user.
/// </summary>
public class TaskItem
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    // Required by EF Core materialisation.
    private TaskItem()
    {
    }

    private TaskItem(
        Guid id,
        string title,
        string? description,
        TaskStatus status,
        DateTime? dueDateUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        Title = title;
        Description = description;
        Status = status;
        DueDateUtc = dueDateUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TaskStatus Status { get; private set; } = TaskStatus.Todo;

    public DateTime? DueDateUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public bool IsReminderSent { get; private set; }

    public static TaskItem Create(
        string title,
        string? description,
        TaskStatus status,
        DateTime? dueDateUtc,
        DateTime utcNow,
        Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new TaskItem(
            id ?? Guid.NewGuid(),
            title.Trim(),
            NormaliseDescription(description),
            status,
            NormaliseUtc(dueDateUtc),
            EnsureUtc(utcNow));
    }

    public void Update(
        string title,
        string? description,
        TaskStatus status,
        DateTime? dueDateUtc,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Description = NormaliseDescription(description);
        Status = status;
        DueDateUtc = NormaliseUtc(dueDateUtc);
        UpdatedAtUtc = EnsureUtc(utcNow);
    }

    public void ChangeStatus(TaskStatus status, DateTime utcNow)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        UpdatedAtUtc = EnsureUtc(utcNow);
    }

    /// <summary>
    /// Idempotent: returns <c>false</c> when a reminder was already recorded, so
    /// repeated Service Bus deliveries or overlapping WebJob runs cannot double-send.
    /// </summary>
    public bool MarkReminderSent(DateTime utcNow)
    {
        if (IsReminderSent)
        {
            return false;
        }

        IsReminderSent = true;
        UpdatedAtUtc = EnsureUtc(utcNow);
        return true;
    }

    public bool IsOverdue(DateTime utcNow) =>
        DueDateUtc is { } due && due <= EnsureUtc(utcNow) && Status != TaskStatus.Completed;

    private static string? NormaliseDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static DateTime? NormaliseUtc(DateTime? value) =>
        value is null ? null : EnsureUtc(value.Value);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
