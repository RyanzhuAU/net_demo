using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;

namespace DemoTaskManager.Application.Abstractions;

/// <summary>
/// Persistence boundary for the <see cref="TaskItem"/> aggregate. All queries are
/// expressed through EF Core LINQ, which is always parameterised - never string SQL.
/// </summary>
public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedAsync(
        TaskQueryParameters query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> GetOverdueUnremindedAsync(
        DateTime utcNow,
        int maxItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims the reminder for a single task. Returns <c>true</c> only for the
    /// caller that flipped <c>IsReminderSent</c> from false to true, which is what makes
    /// overlapping WebJob executions safe.
    /// </summary>
    Task<bool> TryClaimReminderAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken);

    void Add(TaskItem item);

    void Remove(TaskItem item);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
