using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation. Every filter is a LINQ expression, so the provider emits
/// parameterised SQL; no user input is ever concatenated into a command.
/// </summary>
public sealed class TaskRepository(AppDbContext dbContext) : ITaskRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedAsync(
        TaskQueryParameters query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryable = dbContext.Tasks.AsNoTracking().AsQueryable();

        if (query.Status is { } status)
        {
            queryable = queryable.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            queryable = queryable.Where(t => EF.Functions.Like(t.Title, $"%{term}%"));
        }

        var totalCount = await queryable.CountAsync(cancellationToken).ConfigureAwait(false);

        // Deterministic ordering: newest first, Id as the tie-breaker so paging is stable.
        var items = await queryable
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenBy(t => t.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<TaskItem>> GetOverdueUnremindedAsync(
        DateTime utcNow,
        int maxItems,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Tasks
            .AsNoTracking()
            .Where(t => !t.IsReminderSent
                        && t.Status != TaskStatus.Completed
                        && t.DueDateUtc != null
                        && t.DueDateUtc <= utcNow)
            .OrderBy(t => t.DueDateUtc)
            .ThenBy(t => t.Id)
            .Take(maxItems)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items;
    }

    public async Task<bool> TryClaimReminderAsync(Guid id, DateTime utcNow, CancellationToken cancellationToken)
    {
        // Single conditional UPDATE ... WHERE IsReminderSent = 0 - the database decides the winner.
        var affected = await dbContext.Tasks
            .Where(t => t.Id == id && !t.IsReminderSent)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.IsReminderSent, true)
                    .SetProperty(t => t.UpdatedAtUtc, utcNow),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    public void Add(TaskItem item) => dbContext.Tasks.Add(item);

    public void Remove(TaskItem item) => dbContext.Tasks.Remove(item);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
