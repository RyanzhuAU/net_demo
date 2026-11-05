namespace DemoTaskManager.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.TaskItem"/>.
/// </summary>
public enum TaskStatus
{
    Todo = 0,
    InProgress = 1,
    Completed = 2
}
