namespace DemoTaskManager.Application.Options;

public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Maximum number of overdue tasks handled by a single WebJob execution.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Grace period after the due date before a reminder is considered necessary.</summary>
    public int GraceMinutes { get; set; }
}
